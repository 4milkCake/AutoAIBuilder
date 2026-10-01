;;; mascara_camadas_v05.lsp
;;; Versao 0.5.0 - aplicacao controlada de layers semanticas.
;;; Requer mascara_previsualizar_v04.lsp v0.4.2 carregada e preparada.
;;;
;;; SEGURANCA:
;;; - So aplica alteracoes em DWG cujo nome contenha MASCARA ou COPIA.
;;; - Exige confirmacao digitada APLICAR.
;;; - Nao apaga, nao explode e nao altera geometria.
;;; - Nao le nem modifica o interior de CINZA PONTOS.
;;; - Preserva os blocos e agrupa ponto, componentes e textos na mesma layer.
;;; - Guarda as layers anteriores durante a sessao para permitir desfazer.
;;;
;;; Comandos:
;;;   MASCARA_V05_PREPARAR         - executa a preparacao validada da v0.4.2
;;;   MASCARA_V05_STATUS           - informa o plano de organizacao
;;;   MASCARA_V05_PREVISUALIZAR    - destaca tudo que recebera layer semantica
;;;   MASCARA_V05_APLICAR_CAMADAS  - cria layers e organiza os pontos
;;;   MASCARA_V05_DESFAZER_CAMADAS - restaura as layers anteriores da sessao

(vl-load-com)

(setq *mcad5:changes* nil)
(setq *mcad5:created-layers* nil)
(setq *mcad5:last-summary* nil)

(defun mcad5:v04-loaded-p ()
  (and
    (boundp '*mcad4:legend-selection*)
    (boundp '*mcad4:items*)
    (boundp '*mcad4:components*))
)

(defun mcad5:ready-p ()
  (and (mcad5:v04-loaded-p) (mcad4:prepared-p))
)

(defun mcad5:safe-copy-p (/ name)
  (setq name (strcase (getvar "DWGNAME")))
  (or (wcmatch name "*MASCARA*")
      (wcmatch name "*COPIA*"))
)

(defun mcad5:layer-for-info (info / layer-name code)
  (setq layer-name (nth 6 info))
  (setq code (nth 3 info))
  (if (null layer-name) (setq layer-name ""))
  (if (null code) (setq code "SEM_CODIGO"))
  (if (= layer-name "")
    (setq layer-name (strcat "PONTOS_" code)))
  (strcase layer-name)
)

;;; Entrada do plano:
;;; (SEED_HANDLE LAYER DISCIPLINA ENTITY INFO)
(defun mcad5:point-plan (/ plan item entity handle info discipline)
  (setq plan nil)
  (if (mcad5:ready-p)
    (foreach item *mcad4:items*
      (setq entity (nth 0 item))
      (setq handle (nth 1 item))
      (setq info (nth 2 item))
      (setq discipline (nth 1 info))
      (if (and
            (= (nth 2 info) "PONTO_PRINCIPAL")
            (or (= discipline "ELETRICO")
                (= discipline "HIDRAULICO")))
        (setq plan
          (cons
            (list handle (mcad5:layer-for-info info)
                  discipline entity info)
            plan)))))
  plan
)

;;; Entrada unica de layer: (LAYER DISCIPLINA)
(defun mcad5:unique-layers (plan / result entry layer-name)
  (setq result nil)
  (foreach entry plan
    (setq layer-name (nth 1 entry))
    (if (not (assoc layer-name result))
      (setq result
        (cons (list layer-name (nth 2 entry)) result))))
  result
)

(defun mcad5:associated-component-count (plan / count component)
  (setq count 0)
  (foreach component *mcad4:components*
    (if (assoc (nth 4 component) plan)
      (setq count (1+ count))))
  count
)

;;; Retorno: (LAYER_OBJECT CRIADA ERRO)
(defun mcad5:ensure-layer
  (layer-name discipline / document layers result layer color-index)
  (setq document
    (vla-get-ActiveDocument (vlax-get-acad-object)))
  (setq layers (vla-get-Layers document))
  (setq result
    (vl-catch-all-apply 'vla-Item (list layers layer-name)))
  (if (vl-catch-all-error-p result)
    (progn
      (setq result
        (vl-catch-all-apply 'vla-Add (list layers layer-name)))
      (if (vl-catch-all-error-p result)
        (list nil nil (vl-catch-all-error-message result))
        (progn
          (setq layer result)
          (setq color-index
            (cond
              ((= discipline "ELETRICO") 1)
              ((= discipline "HIDRAULICO") 5)
              (T 7)))
          (vl-catch-all-apply 'vla-put-Color
            (list layer color-index))
          (list layer T ""))))
    (list result nil ""))
)

;;; Retorno: (STATUS LAYER_ANTERIOR ERRO)
;;; STATUS pode ser MOVIDO, IGUAL ou ERRO.
(defun mcad5:set-layer
  (entity new-layer / object-result object old-layer change-result)
  (setq object-result
    (vl-catch-all-apply 'vlax-ename->vla-object (list entity)))
  (if (vl-catch-all-error-p object-result)
    (list "ERRO" "" (vl-catch-all-error-message object-result))
    (progn
      (setq object object-result)
      (setq old-layer (vla-get-Layer object))
      (if (= (strcase old-layer) (strcase new-layer))
        (list "IGUAL" old-layer "")
        (progn
          (setq change-result
            (vl-catch-all-apply 'vla-put-Layer
              (list object new-layer)))
          (if (vl-catch-all-error-p change-result)
            (list "ERRO" old-layer
              (vl-catch-all-error-message change-result))
            (list "MOVIDO" old-layer ""))))))
)

(defun mcad5:move-and-record
  (entity new-layer / result)
  (setq result (mcad5:set-layer entity new-layer))
  (if (= (nth 0 result) "MOVIDO")
    (setq *mcad5:changes*
      (cons (list entity (nth 1 result) new-layer)
            *mcad5:changes*)))
  result
)

(defun c:MASCARA_V05_PREPARAR ()
  (if (not (mcad5:v04-loaded-p))
    (prompt
      "\nCarregue primeiro mascara_previsualizar_v04.lsp v0.4.2.")
    (c:MASCARA_V04_PREPARAR))
  (princ)
)

(defun c:MASCARA_V05_STATUS
  (/ plan layers point-count component-count)
  (if (not (mcad5:ready-p))
    (prompt
      "\nExecute primeiro MASCARA_V05_PREPARAR e selecione a legenda.")
    (progn
      (setq plan (mcad5:point-plan))
      (setq layers (mcad5:unique-layers plan))
      (setq point-count (length plan))
      (setq component-count (mcad5:associated-component-count plan))
      (prompt "\nPlano de layers semanticas v0.5:")
      (prompt
        (strcat "\nPontos principais: " (itoa point-count)
                " | componentes/textos: " (itoa component-count)
                " | total: " (itoa (+ point-count component-count))))
      (prompt
        (strcat "\nLayers semanticas distintas: "
                (itoa (length layers))))
      (if (mcad5:safe-copy-p)
        (prompt "\nNome do DWG autorizado para aplicacao.")
        (prompt
          "\nATENCAO: use SALVAR COMO e inclua MASCARA ou COPIA no nome."))))
  (princ)
)

(defun c:MASCARA_V05_PREVISUALIZAR ()
  (if (not (mcad5:ready-p))
    (prompt
      "\nExecute primeiro MASCARA_V05_PREPARAR e selecione a legenda.")
    (c:MASCARA_V04_PONTOS))
  (princ)
)

(defun c:MASCARA_V05_APLICAR_CAMADAS
  (/ *error* old-error document undo-open answer plan layer-plan layer-entry
     layer-name discipline layer-result item entry result component
     seed-entry created-count existing-count layer-error-count moved-points
     moved-components same-count move-error-count)

  (setq old-error *error*)
  (setq document
    (vla-get-ActiveDocument (vlax-get-acad-object)))
  (setq undo-open nil)

  (defun *error* (message)
    (if undo-open
      (progn
        (vl-catch-all-apply 'vla-EndUndoMark (list document))
        (setq undo-open nil)))
    (setq *error* old-error)
    (if (and message
             (/= message "Function cancelled")
             (/= message "quit / exit abort"))
      (prompt
        (strcat "\nErro em MASCARA_V05_APLICAR_CAMADAS: " message)))
    (princ))

  (cond
    ((not (mcad5:v04-loaded-p))
      (prompt
        "\nCarregue primeiro mascara_previsualizar_v04.lsp v0.4.2."))
    ((not (mcad5:ready-p))
      (prompt
        "\nExecute primeiro MASCARA_V05_PREPARAR e selecione a legenda."))
    ((not (mcad5:safe-copy-p))
      (prompt
        "\nBLOQUEADO: salve uma copia cujo nome contenha MASCARA ou COPIA."))
    ((or *mcad5:changes* *mcad5:created-layers*)
      (prompt
        "\nHa alteracoes pendentes. Use MASCARA_V05_DESFAZER_CAMADAS antes de reaplicar."))
    (T
      (setq answer
        (strcase
          (getstring T
            "\nDigite APLICAR para criar as layers semanticas nesta COPIA: ")))
      (if (/= answer "APLICAR")
        (prompt "\nOperacao cancelada. O desenho nao foi alterado.")
        (progn
          (setq plan (mcad5:point-plan))
          (setq layer-plan (mcad5:unique-layers plan))
          (setq *mcad5:changes* nil)
          (setq *mcad5:created-layers* nil)
          (setq created-count 0 existing-count 0 layer-error-count 0)
          (setq moved-points 0 moved-components 0
                same-count 0 move-error-count 0)

          (vl-catch-all-apply 'vla-StartUndoMark (list document))
          (setq undo-open T)

          ;; Cria somente as layers que ainda nao existem.
          (foreach layer-entry layer-plan
            (setq layer-name (nth 0 layer-entry))
            (setq discipline (nth 1 layer-entry))
            (setq layer-result
              (mcad5:ensure-layer layer-name discipline))
            (cond
              ((nth 1 layer-result)
                (setq created-count (1+ created-count))
                (setq *mcad5:created-layers*
                  (cons layer-name *mcad5:created-layers*)))
              ((nth 0 layer-result)
                (setq existing-count (1+ existing-count)))
              (T
                (setq layer-error-count (1+ layer-error-count)))))

          ;; Organiza os blocos principais.
          (foreach entry plan
            (setq layer-name (nth 1 entry))
            (setq result
              (mcad5:move-and-record (nth 3 entry) layer-name))
            (cond
              ((= (nth 0 result) "MOVIDO")
                (setq moved-points (1+ moved-points)))
              ((= (nth 0 result) "IGUAL")
                (setq same-count (1+ same-count)))
              (T
                (setq move-error-count (1+ move-error-count)))))

          ;; Coloca cada componente ou texto na layer do seu ponto principal.
          (foreach component *mcad4:components*
            (setq seed-entry (assoc (nth 4 component) plan))
            (if seed-entry
              (progn
                (setq layer-name (nth 1 seed-entry))
                (setq result
                  (mcad5:move-and-record (nth 0 component) layer-name))
                (cond
                  ((= (nth 0 result) "MOVIDO")
                    (setq moved-components (1+ moved-components)))
                  ((= (nth 0 result) "IGUAL")
                    (setq same-count (1+ same-count)))
                  (T
                    (setq move-error-count (1+ move-error-count)))))))

          (vl-catch-all-apply 'vla-EndUndoMark (list document))
          (setq undo-open nil)
          (vl-catch-all-apply 'vla-Regen (list document 1))

          (setq *mcad5:last-summary*
            (list created-count existing-count layer-error-count
                  moved-points moved-components same-count move-error-count))

          (prompt "\nAplicacao de layers semanticas concluida.")
          (prompt
            (strcat "\nLayers criadas: " (itoa created-count)
                    " | existentes: " (itoa existing-count)
                    " | erros de layer: " (itoa layer-error-count)))
          (prompt
            (strcat "\nPontos movidos: " (itoa moved-points)
                    " | componentes/textos movidos: "
                    (itoa moved-components)
                    " | ja organizados: " (itoa same-count)
                    " | erros: " (itoa move-error-count)))
          (prompt
            "\nNenhuma entidade foi apagada ou explodida. CINZA PONTOS permaneceu intacto.")))))

  (setq *error* old-error)
  (princ)
)

(defun c:MASCARA_V05_DESFAZER_CAMADAS
  (/ document changes change result restored-count error-count remaining
     layers layer-name layer-result deleted-count remaining-layers)
  (if (and (null *mcad5:changes*) (null *mcad5:created-layers*))
    (prompt "\nNao ha alteracoes da v0.5 para desfazer nesta sessao.")
    (progn
      (setq document
        (vla-get-ActiveDocument (vlax-get-acad-object)))
      (setq changes *mcad5:changes*)
      (setq restored-count 0 error-count 0 remaining nil)

      (foreach change changes
        (setq result (mcad5:set-layer (nth 0 change) (nth 1 change)))
        (if (or (= (nth 0 result) "MOVIDO")
                (= (nth 0 result) "IGUAL"))
          (setq restored-count (1+ restored-count))
          (progn
            (setq error-count (1+ error-count))
            (setq remaining (cons change remaining)))))
      (setq *mcad5:changes* remaining)

      ;; Remove apenas layers criadas pela v0.5 que ficaram vazias.
      (setq layers (vla-get-Layers document))
      (setq deleted-count 0 remaining-layers nil)
      (foreach layer-name *mcad5:created-layers*
        (setq layer-result
          (vl-catch-all-apply 'vla-Item (list layers layer-name)))
        (if (vl-catch-all-error-p layer-result)
          (setq deleted-count (1+ deleted-count))
          (progn
            (setq result
              (vl-catch-all-apply 'vla-Delete (list layer-result)))
            (if (vl-catch-all-error-p result)
              (setq remaining-layers
                (cons layer-name remaining-layers))
              (setq deleted-count (1+ deleted-count))))))
      (setq *mcad5:created-layers* remaining-layers)
      (if (and (null *mcad5:changes*)
               (null *mcad5:created-layers*))
        (setq *mcad5:last-summary* nil))
      (vl-catch-all-apply 'vla-Regen (list document 1))

      (prompt
        (strcat "\nLayers anteriores restauradas em "
                (itoa restored-count) " objetos."))
      (prompt
        (strcat "\nLayers novas removidas: " (itoa deleted-count)
                " | erros de restauracao: " (itoa error-count)))
      (if (or *mcad5:changes* *mcad5:created-layers*)
        (prompt
          "\nAlguns itens nao puderam ser restaurados. Use o comando UNDO do AutoCAD nesta copia."))))
  (princ)
)

(prompt
  "\nRotina v0.5.0 carregada. Use MASCARA_V05_PREPARAR para iniciar."
)
(princ)
