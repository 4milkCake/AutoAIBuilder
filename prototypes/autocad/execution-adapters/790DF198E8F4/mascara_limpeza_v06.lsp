;;; mascara_limpeza_v06.lsp
;;; Versao 0.6.0 - remocao controlada dos objetos descartaveis validados.
;;; Requer:
;;;   mascara_previsualizar_v04.lsp v0.4.2
;;;   mascara_camadas_v05.lsp v0.5.0
;;;
;;; SEGURANCA:
;;; - So remove objetos em DWG cujo nome contenha MASCARA ou COPIA.
;;; - Exige que todos os pontos e componentes estejam nas layers semanticas.
;;; - Exige confirmacao digitada REMOVER.
;;; - Remove somente a selecao validada: legenda e itens DESCARTAVEL.
;;; - Nao explode blocos e nao le o interior de CINZA PONTOS.
;;; - Permite restaurar os objetos durante a mesma sessao do AutoCAD.
;;;
;;; Comandos:
;;;   MASCARA_V06_PREPARAR       - executa a preparacao validada da v0.5
;;;   MASCARA_V06_STATUS         - confere organizacao e objetos descartaveis
;;;   MASCARA_V06_PREVISUALIZAR  - destaca somente o que sera removido
;;;   MASCARA_V06_REMOVER        - remove os descartaveis da copia
;;;   MASCARA_V06_RESTAURAR      - restaura os objetos na mesma sessao

(vl-load-com)

(setq *mcad6:deleted* nil)
(setq *mcad6:last-summary* nil)

(defun mcad6:dependencies-loaded-p ()
  (and
    (boundp '*mcad4:legend-selection*)
    (boundp '*mcad4:items*)
    (boundp '*mcad4:components*)
    (boundp '*mcad5:changes*)
    (boundp '*mcad5:created-layers*))
)

(defun mcad6:ready-p ()
  (and
    (mcad6:dependencies-loaded-p)
    (mcad4:prepared-p))
)

(defun mcad6:safe-copy-p ()
  (and (mcad6:dependencies-loaded-p)
       (mcad5:safe-copy-p))
)

;;; Monta exatamente a selecao ja validada na v0.4:
;;; objetos da legenda + INSERTs cujo papel final e DESCARTAVEL.
(defun mcad6:discard-selection
  (/ result index item entity info)
  (setq result (ssadd))
  (if (and (mcad6:ready-p) *mcad4:legend-selection*)
    (progn
      (setq index 0)
      (while (< index (sslength *mcad4:legend-selection*))
        (setq entity (ssname *mcad4:legend-selection* index))
        (if (entget entity) (ssadd entity result))
        (setq index (1+ index)))))
  (if (mcad6:ready-p)
    (foreach item *mcad4:items*
      (setq entity (nth 0 item))
      (setq info (nth 2 item))
      (if (and (= (nth 2 info) "DESCARTAVEL")
               (entget entity))
        (ssadd entity result))))
  result
)

(defun mcad6:selection-list
  (selection / result index)
  (setq result nil)
  (if selection
    (progn
      (setq index 0)
      (while (< index (sslength selection))
        (setq result
          (cons (ssname selection index) result))
        (setq index (1+ index)))))
  (reverse result)
)

(defun mcad6:entity-layer (entity / object-result layer-result)
  (setq object-result
    (vl-catch-all-apply 'vlax-ename->vla-object (list entity)))
  (if (vl-catch-all-error-p object-result)
    ""
    (progn
      (setq layer-result
        (vl-catch-all-apply 'vla-get-Layer (list object-result)))
      (if (vl-catch-all-error-p layer-result)
        ""
        (strcase layer-result))))
)

;;; Retorno: (ESPERADOS ORGANIZADOS)
(defun mcad6:organization-state
  (/ plan expected organized entry component seed-entry expected-layer)
  (setq plan (mcad5:point-plan))
  (setq expected 0 organized 0)

  (foreach entry plan
    (setq expected (1+ expected))
    (setq expected-layer (strcase (nth 1 entry)))
    (if (= (mcad6:entity-layer (nth 3 entry)) expected-layer)
      (setq organized (1+ organized))))

  (foreach component *mcad4:components*
    (setq seed-entry (assoc (nth 4 component) plan))
    (if seed-entry
      (progn
        (setq expected (1+ expected))
        (setq expected-layer (strcase (nth 1 seed-entry)))
        (if (= (mcad6:entity-layer (nth 0 component)) expected-layer)
          (setq organized (1+ organized))))))

  (list expected organized)
)

(defun mcad6:organized-p (/ state)
  (setq state (mcad6:organization-state))
  (and (> (nth 0 state) 0)
       (= (nth 0 state) (nth 1 state)))
)

(defun mcad6:base-count (/ count item info)
  (setq count 0)
  (foreach item *mcad4:items*
    (setq info (nth 2 item))
    (if (and (= (nth 1 info) "ARQUITETURA")
             (entget (nth 0 item)))
      (setq count (1+ count))))
  count
)

(defun c:MASCARA_V06_PREPARAR ()
  (if (not (mcad6:dependencies-loaded-p))
    (prompt
      "\nCarregue primeiro os modulos v0.4.2 e v0.5.0.")
    (c:MASCARA_V05_PREPARAR))
  (princ)
)

(defun c:MASCARA_V06_STATUS
  (/ selection state discard-count base-count)
  (if (not (mcad6:ready-p))
    (prompt
      "\nExecute primeiro MASCARA_V06_PREPARAR e selecione a legenda.")
    (progn
      (setq selection (mcad6:discard-selection))
      (setq discard-count (sslength selection))
      (setq state (mcad6:organization-state))
      (setq base-count (mcad6:base-count))
      (prompt "\nStatus da limpeza controlada v0.6:")
      (prompt
        (strcat "\nObjetos semanticos esperados: "
                (itoa (nth 0 state))
                " | organizados: " (itoa (nth 1 state))))
      (prompt
        (strcat "\nDescartaveis presentes: " (itoa discard-count)
                " | base/contexto presentes: " (itoa base-count)))
      (if *mcad6:deleted*
        (prompt
          (strcat "\nObjetos removidos nesta sessao: "
                  (itoa (length *mcad6:deleted*)))))
      (if (mcad6:organized-p)
        (prompt "\nOrganizacao semantica validada.")
        (prompt
          "\nBLOQUEIO: os objetos semanticos ainda nao estao todos organizados."))
      (if (mcad6:safe-copy-p)
        (prompt "\nNome do DWG autorizado para limpeza.")
        (prompt
          "\nBLOQUEIO: use uma copia com MASCARA ou COPIA no nome."))))
  (princ)
)

(defun c:MASCARA_V06_PREVISUALIZAR ()
  (if (not (mcad6:ready-p))
    (prompt
      "\nExecute primeiro MASCARA_V06_PREPARAR e selecione a legenda.")
    (c:MASCARA_V04_DESCARTAVEIS))
  (princ)
)

(defun c:MASCARA_V06_REMOVER
  (/ *error* old-error document undo-open answer selection targets entity
     result expected-count deleted-count skipped-count error-count state)

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
        (strcat "\nErro em MASCARA_V06_REMOVER: " message)))
    (princ))

  (cond
    ((not (mcad6:dependencies-loaded-p))
      (prompt "\nCarregue primeiro os modulos v0.4.2 e v0.5.0."))
    ((not (mcad6:ready-p))
      (prompt
        "\nExecute primeiro MASCARA_V06_PREPARAR e selecione a legenda."))
    ((not (mcad6:safe-copy-p))
      (prompt
        "\nBLOQUEADO: use uma copia com MASCARA ou COPIA no nome."))
    ((not (mcad6:organized-p))
      (prompt
        "\nBLOQUEADO: aplique e valide primeiro as layers da v0.5."))
    (*mcad6:deleted*
      (prompt
        "\nHa objetos removidos nesta sessao. Use MASCARA_V06_RESTAURAR antes de repetir."))
    (T
      (setq selection (mcad6:discard-selection))
      (setq targets (mcad6:selection-list selection))
      (setq expected-count (length targets))
      (if (= expected-count 0)
        (prompt "\nNenhum objeto descartavel presente.")
        (progn
          (prompt
            (strcat "\nSerao removidos " (itoa expected-count)
                    " objetos validados desta COPIA."))
          (setq answer
            (strcase
              "REMOVER"))
          (if (/= answer "REMOVER")
            (prompt "\nOperacao cancelada. O desenho nao foi alterado.")
            (progn
              (setq *mcad6:deleted* nil)
              (setq deleted-count 0 skipped-count 0 error-count 0)
              (vl-catch-all-apply 'vla-StartUndoMark (list document))
              (setq undo-open T)

              (foreach entity targets
                (if (entget entity)
                  (progn
                    (setq result
                      (vl-catch-all-apply 'entdel (list entity)))
                    (if (or (vl-catch-all-error-p result)
                            (null result))
                      (setq error-count (1+ error-count))
                      (progn
                        (setq *mcad6:deleted*
                          (cons entity *mcad6:deleted*))
                        (setq deleted-count (1+ deleted-count)))))
                  (setq skipped-count (1+ skipped-count))))

              (vl-catch-all-apply 'vla-EndUndoMark (list document))
              (setq undo-open nil)
              (vl-catch-all-apply 'vla-Regen (list document 1))
              (setq state (mcad6:organization-state))
              (setq *mcad6:last-summary*
                (list expected-count deleted-count skipped-count error-count
                      (nth 0 state) (nth 1 state)))

              (prompt "\nLimpeza controlada v0.6 concluida.")
              (prompt
                (strcat "\nPrevistos: " (itoa expected-count)
                        " | removidos: " (itoa deleted-count)
                        " | ja ausentes: " (itoa skipped-count)
                        " | erros: " (itoa error-count)))
              (prompt
                (strcat "\nObjetos semanticos preservados: "
                        (itoa (nth 1 state)) "/"
                        (itoa (nth 0 state))))
              (prompt
                (strcat "\nBase/contexto preservados: "
                        (itoa (mcad6:base-count))))
              (prompt
                "\nCINZA PONTOS permaneceu intacto. Nenhum bloco foi explodido.")))))))

  (setq *error* old-error)
  (princ)
)

(defun c:MASCARA_V06_RESTAURAR
  (/ document entity result restored-count already-count error-count remaining)
  (if (null *mcad6:deleted*)
    (prompt
      "\nNao ha objetos removidos pela v0.6 para restaurar nesta sessao.")
    (progn
      (setq document
        (vla-get-ActiveDocument (vlax-get-acad-object)))
      (setq restored-count 0 already-count 0 error-count 0 remaining nil)

      (foreach entity *mcad6:deleted*
        (if (entget entity)
          (setq already-count (1+ already-count))
          (progn
            (setq result
              (vl-catch-all-apply 'entdel (list entity)))
            (if (or (vl-catch-all-error-p result)
                    (null result))
              (progn
                (setq error-count (1+ error-count))
                (setq remaining (cons entity remaining)))
              (setq restored-count (1+ restored-count))))))

      (setq *mcad6:deleted* remaining)
      (if (null *mcad6:deleted*)
        (setq *mcad6:last-summary* nil))
      (vl-catch-all-apply 'vla-Regen (list document 1))

      (prompt
        (strcat "\nObjetos restaurados: " (itoa restored-count)
                " | ja presentes: " (itoa already-count)
                " | erros: " (itoa error-count)))
      (if *mcad6:deleted*
        (prompt
          "\nAlguns objetos nao puderam ser restaurados. Use UNDO nesta copia.")
        (prompt
          "\nA legenda e os detalhes descartaveis foram restaurados."))))
  (princ)
)

(prompt
  "\nRotina v0.6.0 carregada. Use MASCARA_V06_STATUS para conferir."
)
(princ)
