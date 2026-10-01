;;; mascara_previsualizar_v04.lsp
;;; Versao 0.4.2 - Pre-visualizacao operacional da mascara.
;;; Preparada para o desenho de teste analisado no AutoCAD 2025 completo.
;;;
;;; IMPORTANTE:
;;; - Nao apaga, move, explode, cria layers ou altera objetos do desenho.
;;; - Apenas analisa, gera CSV e cria selecoes temporarias na tela.
;;; - As futuras layers REF_* aparecem no relatorio como proposta.
;;;
;;; Comandos:
;;;   MASCARA_V03_ANALISAR       - gera mapa semantico CSV por objeto
;;;   MASCARA_V03_PONTOS         - destaca todos os pontos de instalacoes
;;;   MASCARA_V03_ELETRICO       - destaca pontos eletricos
;;;   MASCARA_V03_HIDRAULICO     - destaca pontos hidraulicos
;;;   MASCARA_V03_SANITARIO      - destaca pontos sanitarios
;;;   MASCARA_V03_ARQUITETURA    - destaca base, mobiliario e contexto
;;;   MASCARA_V03_DESCARTAVEIS   - destaca elementos dispensaveis
;;;   MASCARA_V03_REVISAR        - destaca objetos ainda nao classificados
;;;   MASCARA_V03_LIMPAR         - limpa a selecao temporaria
;;;   MASCARA_V033_ANALISAR      - aplica o dicionario confirmado aos blocos
;;;   MASCARA_V033_LER_LEGENDA   - extrai objetos e pares bloco/texto da legenda
;;;   MASCARA_V034_ANALISAR      - aprende a legenda e exclui seus simbolos
;;;   MASCARA_V035_ANALISAR      - fecha o mapa com contexto e revisoes visuais
;;;   MASCARA_V04_PREPARAR       - prepara selecoes da futura mascara
;;;   MASCARA_V04_ELETRICO       - destaca pontos eletricos completos
;;;   MASCARA_V04_HIDRAULICO     - destaca pontos hidraulicos completos
;;;   MASCARA_V04_ELETRICO_BLOCOS - diagnostico: somente blocos principais
;;;   MASCARA_V04_HIDRAULICO_BLOCOS - diagnostico: somente blocos principais
;;;   MASCARA_V04_BASE           - destaca base e contexto preservados
;;;   MASCARA_V04_DESCARTAVEIS   - destaca legenda e detalhes dispensaveis
;;;   MASCARA_V04_TIPO           - destaca um codigo semantico especifico

(defun mcad3:contains (text fragment)
  (and text fragment
       (wcmatch (strcase text) (strcat "*" (strcase fragment) "*")))
)

(defun mcad3:csv-field (value / text result index char)
  (setq text (if value value ""))
  (setq result "\"" index 1)
  (while (<= index (strlen text))
    (setq char (substr text index 1))
    (if (= char "\"")
      (setq result (strcat result "\"\""))
      (setq result (strcat result char))
    )
    (setq index (1+ index))
  )
  (strcat result "\"")
)

(defun mcad3:base-name (file-name / length-name extension)
  (setq length-name (strlen file-name))
  (if (> length-name 4)
    (setq extension (strcase (substr file-name (- length-name 3))))
    (setq extension "")
  )
  (if (= extension ".DWG")
    (substr file-name 1 (- length-name 4))
    file-name
  )
)

(defun mcad3:block-name (entity-data entity-type / value)
  (setq value "")
  (if (= entity-type "INSERT")
    (progn
      (setq value (cdr (assoc 2 entity-data)))
      (if (null value) (setq value ""))
    )
  )
  value
)

(defun mcad3:classification
  (entity-data / entity-type layer-name block-name type-upper layer-upper block-upper)

  (setq entity-type (cdr (assoc 0 entity-data)))
  (setq layer-name (cdr (assoc 8 entity-data)))
  (if (null entity-type) (setq entity-type ""))
  (if (null layer-name) (setq layer-name ""))

  (setq block-name (mcad3:block-name entity-data entity-type))
  (setq type-upper (strcase entity-type))
  (setq layer-upper (strcase layer-name))
  (setq block-upper (strcase block-name))

  (cond
    ;; Blocos inspecionados visualmente e confirmados como arquitetura.
    ((= block-upper "CINZA PONTOS")
      (list "REF_ARQ_BASE" "ALTA" "MANTER"
            "Base arquitetonica completa confirmada visualmente"))

    ((= block-upper "RFWERF")
      (list "REF_ARQ_MOBILIARIO" "ALTA" "MANTER"
            "Cama e criados-mudos confirmados visualmente"))

    ((= block-upper "BASE 1")
      (list "REF_ARQ_BASE" "MEDIA" "MANTER"
            "Bloco de base preservado no fluxo manual"))

    ;; Elementos dispensaveis ja validados no relatorio anterior.
    ((or (= type-upper "DIMENSION")
         (= type-upper "LEADER")
         (and (= layer-upper "CHAMADA - COTAS")
              (= type-upper "LINE"))
         (= layer-upper "TABELA")
         (= layer-upper "DEFPOINTS")
         (= block-upper "LOGO ICON")
         (= block-upper "NOME DO_DESENHO"))
      (list "DESCARTAVEL" "ALTA" "OCULTAR_OU_REMOVER"
            "Cota, chamada, tabela ou auxiliar confirmado como dispensavel"))

    ;; Registro hidraulico confirmado pelo nome do bloco.
    ((= block-upper "SIMBOLO REGISTRO")
      (list "REF_HID_REGISTRO" "ALTA" "CLASSIFICADO"
            "Bloco identificado como registro hidraulico"))

    ;; Pontos originados nos layers de simbologia do projeto.
    ((and (mcad3:contains layer-upper "SIMBOLOGIA PONTOS")
          (mcad3:contains layer-upper "HIDR"))
      (list "REF_HID_PONTO_GERAL" "MEDIA" "CLASSIFICAR_SUBTIPO"
            "Ponto em layer de simbologia hidraulica; subtipo ainda nao definido"))

    ((mcad3:contains layer-upper "SIMBOLOGIA PONTOS")
      (list "REF_ELE_PONTO_GERAL" "MEDIA" "CLASSIFICAR_SUBTIPO"
            "Ponto em layer de simbologia eletrica; subtipo ainda nao definido"))

    ((and (mcad3:contains layer-upper "HID -")
          (mcad3:contains layer-upper "ESGOTO"))
      (list "REF_SAN_PONTO_GERAL" "MEDIA" "CLASSIFICAR_SUBTIPO"
            "Elemento em layer de esgoto; subtipo ainda nao definido"))

    ((mcad3:contains layer-upper "HID -")
      (list "REF_HID_PONTO_GERAL" "MEDIA" "CLASSIFICAR_SUBTIPO"
            "Elemento em layer hidraulico; subtipo ainda nao definido"))

    ;; Mobiliario e loucas sao contexto semantico para os projetos.
    ((mcad3:contains layer-upper "MOBILI")
      (list "REF_ARQ_MOBILIARIO" "ALTA" "MANTER_CONTEXTO"
            "Mobiliario ou louca util para reconhecer ambientes e equipamentos"))

    ;; Projecoes, hachuras e textos sao mantidos como contexto arquitetonico.
    ((or (mcad3:contains layer-upper "PROJE")
         (mcad3:contains layer-upper "HACH")
         (mcad3:contains layer-upper "TEXT")
         (mcad3:contains layer-upper "CHAMADA - T")
         (= layer-upper "COR 10")
         (= layer-upper "TX-3")
         (= type-upper "TEXT")
         (= type-upper "MTEXT")
         (= type-upper "HATCH"))
      (list "REF_ARQ_CONTEXTO" "MEDIA" "MANTER_CONTEXTO"
            "Texto, titulo, projecao ou representacao util para interpretar a planta"))

    (T
      (list "REF_REVISAR" "BAIXA" "REVISAR_MANUALMENTE"
            "Objeto em layer generico ou sem regra semantica confirmada"))
  )
)

(defun mcad3:wanted-p (wanted category)
  (cond
    ((= wanted "TODOS") T)
    ((= wanted category) T)
    ((and (= wanted "PONTOS")
          (member category
            '("REF_ELE_PONTO_GERAL"
              "REF_HID_PONTO_GERAL"
              "REF_HID_REGISTRO"
              "REF_SAN_PONTO_GERAL")))
      T)
    ((and (= wanted "HIDRAULICO")
          (member category
            '("REF_HID_PONTO_GERAL" "REF_HID_REGISTRO")))
      T)
    ((and (= wanted "ARQUITETURA")
          (member category
            '("REF_ARQ_BASE" "REF_ARQ_MOBILIARIO" "REF_ARQ_CONTEXTO")))
      T)
    (T nil)
  )
)

(defun mcad3:collect
  (wanted / source result index entity entity-data classification category)

  (setq result (ssadd))
  (setq source (ssget "_X" '((410 . "Model"))))

  (if source
    (progn
      (setq index 0)
      (while (< index (sslength source))
        (setq entity (ssname source index))
        (setq entity-data (entget entity))
        (setq classification (mcad3:classification entity-data))
        (setq category (nth 0 classification))

        (if (mcad3:wanted-p wanted category)
          (ssadd entity result)
        )
        (setq index (1+ index))
      )
    )
  )
  result
)

(defun mcad3:preview (wanted title expected / selection count)
  (sssetfirst nil nil)
  (setq selection (mcad3:collect wanted))
  (setq count (sslength selection))

  (if (> count 0)
    (progn
      (sssetfirst nil selection)
      (prompt (strcat "\n" title ": " (itoa count) " objetos selecionados temporariamente."))
      (if (= count expected)
        (prompt " Contagem igual ao desenho de teste analisado.")
        (prompt
          (strcat " ATENCAO: eram esperados " (itoa expected) " objetos neste teste."))
      )
      (prompt "\nNenhum objeto foi alterado. Pressione ESC ou use MASCARA_V03_LIMPAR.")
    )
    (prompt "\nNenhum objeto encontrado. O desenho nao foi modificado.")
  )
  (princ)
)

(defun mcad3:add-count (key counts / found)
  (if (setq found (assoc key counts))
    (subst (cons key (1+ (cdr found))) found counts)
    (cons (cons key 1) counts)
  )
)

(defun mcad3:count-value (key counts / found)
  (if (setq found (assoc key counts)) (cdr found) 0)
)

(defun mcad3:pad-number (number width / text)
  (setq text (itoa number))
  (while (< (strlen text) width)
    (setq text (strcat "0" text))
  )
  text
)

(defun mcad3:number-text (value)
  (if (numberp value) (rtos value 2 6) "")
)

(defun mcad3:point-value (point index)
  (if (and point (> (length point) index))
    (nth index point)
    0.0
  )
)

(defun c:MASCARA_V03_ANALISAR
  (/ *error* old-error file-handle output-file drawing-path drawing-name
     default-output source index entity data handle layer-name entity-type
     block-name point rotation scale-x scale-y classification category
     confidence action reason counts sequence category-number semantic-id
     total)

  (setq old-error *error*)
  (defun *error* (message)
    (if file-handle
      (progn (close file-handle) (setq file-handle nil))
    )
    (setq *error* old-error)
    (if (and message
             (/= message "Function cancelled")
             (/= message "quit / exit abort"))
      (prompt (strcat "\nErro em MASCARA_V03_ANALISAR: " message))
    )
    (princ)
  )

  (setq drawing-path (getvar "DWGPREFIX"))
  (setq drawing-name (getvar "DWGNAME"))

  (if (= drawing-path "")
    (prompt "\nSalve uma copia do DWG antes de executar a analise.")
    (progn
      (setq default-output
        (strcat drawing-path
                (mcad3:base-name drawing-name)
                "_mapa_semantico_v03.csv"))
      (setq output-file
        (getfiled "Salvar mapa semantico da mascara" default-output "csv" 1))

      (if output-file
        (progn
          (setq source (ssget "_X" '((410 . "Model"))))
          (setq file-handle (open output-file "w"))
          (setq counts nil sequence nil total 0)

          (if file-handle
            (progn
              (write-line
                "\"ARQUIVO\";\"ID_SEMANTICO\";\"HANDLE\";\"LAYER_ORIGINAL\";\"TIPO_OBJETO\";\"NOME_BLOCO\";\"X\";\"Y\";\"Z\";\"ROTACAO_GRAUS\";\"ESCALA_X\";\"ESCALA_Y\";\"CATEGORIA_SEMANTICA\";\"CONFIANCA\";\"ACAO_PROPOSTA\";\"MOTIVO\""
                file-handle)

              (if source
                (progn
                  (setq index 0)
                  (while (< index (sslength source))
                    (setq entity (ssname source index))
                    (setq data (entget entity))
                    (setq handle (cdr (assoc 5 data)))
                    (setq layer-name (cdr (assoc 8 data)))
                    (setq entity-type (cdr (assoc 0 data)))
                    (if (null handle) (setq handle ""))
                    (if (null layer-name) (setq layer-name ""))
                    (if (null entity-type) (setq entity-type ""))
                    (setq block-name (mcad3:block-name data entity-type))
                    (setq point (cdr (assoc 10 data)))
                    (setq rotation (cdr (assoc 50 data)))
                    (setq scale-x (cdr (assoc 41 data)))
                    (setq scale-y (cdr (assoc 42 data)))
                    (if (null rotation) (setq rotation 0.0))
                    (if (null scale-x) (setq scale-x 1.0))
                    (if (null scale-y) (setq scale-y 1.0))

                    (setq classification (mcad3:classification data))
                    (setq category (nth 0 classification))
                    (setq confidence (nth 1 classification))
                    (setq action (nth 2 classification))
                    (setq reason (nth 3 classification))
                    (setq counts (mcad3:add-count category counts))
                    (setq sequence (mcad3:add-count category sequence))
                    (setq category-number (mcad3:count-value category sequence))
                    (setq semantic-id
                      (strcat category "-" (mcad3:pad-number category-number 4)))

                    (write-line
                      (strcat
                        (mcad3:csv-field drawing-name) ";"
                        (mcad3:csv-field semantic-id) ";"
                        (mcad3:csv-field handle) ";"
                        (mcad3:csv-field layer-name) ";"
                        (mcad3:csv-field entity-type) ";"
                        (mcad3:csv-field block-name) ";"
                        (mcad3:csv-field (mcad3:number-text (mcad3:point-value point 0))) ";"
                        (mcad3:csv-field (mcad3:number-text (mcad3:point-value point 1))) ";"
                        (mcad3:csv-field (mcad3:number-text (mcad3:point-value point 2))) ";"
                        (mcad3:csv-field (mcad3:number-text (* rotation (/ 180.0 pi)))) ";"
                        (mcad3:csv-field (mcad3:number-text scale-x)) ";"
                        (mcad3:csv-field (mcad3:number-text scale-y)) ";"
                        (mcad3:csv-field category) ";"
                        (mcad3:csv-field confidence) ";"
                        (mcad3:csv-field action) ";"
                        (mcad3:csv-field reason))
                      file-handle)

                    (setq total (1+ total))
                    (setq index (1+ index))
                  )
                )
              )

              (close file-handle)
              (setq file-handle nil)
              (prompt (strcat "\nMapa semantico concluido: " (itoa total) " objetos."))
              (prompt
                (strcat
                  "\nELE=" (itoa (mcad3:count-value "REF_ELE_PONTO_GERAL" counts))
                  " | HID="
                  (itoa (+ (mcad3:count-value "REF_HID_PONTO_GERAL" counts)
                           (mcad3:count-value "REF_HID_REGISTRO" counts)))
                  " | SAN=" (itoa (mcad3:count-value "REF_SAN_PONTO_GERAL" counts))))
              (prompt
                (strcat
                  "\nARQ_BASE=" (itoa (mcad3:count-value "REF_ARQ_BASE" counts))
                  " | MOBILIARIO="
                  (itoa (mcad3:count-value "REF_ARQ_MOBILIARIO" counts))
                  " | CONTEXTO="
                  (itoa (mcad3:count-value "REF_ARQ_CONTEXTO" counts))))
              (prompt
                (strcat
                  "\nDESCARTAVEIS=" (itoa (mcad3:count-value "DESCARTAVEL" counts))
                  " | REVISAR=" (itoa (mcad3:count-value "REF_REVISAR" counts))))
              (prompt (strcat "\nRelatorio salvo em: " output-file))
              (prompt "\nO desenho nao foi modificado e nenhuma layer foi criada.")
            )
            (prompt "\nNao foi possivel criar o CSV selecionado.")
          )
        )
        (prompt "\nOperacao cancelada. Nenhum arquivo foi criado.")
      )
    )
  )
  (setq *error* old-error)
  (princ)
)

(defun c:MASCARA_V03_PONTOS ()
  (mcad3:preview "PONTOS" "TODOS OS PONTOS DE INSTALACOES" 432)
)

(defun c:MASCARA_V03_ELETRICO ()
  (mcad3:preview "REF_ELE_PONTO_GERAL" "PONTOS ELETRICOS" 311)
)

(defun c:MASCARA_V03_HIDRAULICO ()
  (mcad3:preview "HIDRAULICO" "PONTOS HIDRAULICOS" 119)
)

(defun c:MASCARA_V03_SANITARIO ()
  (mcad3:preview "REF_SAN_PONTO_GERAL" "PONTOS SANITARIOS" 2)
)

(defun c:MASCARA_V03_ARQUITETURA ()
  (mcad3:preview "ARQUITETURA" "ARQUITETURA, MOBILIARIO E CONTEXTO" 474)
)

(defun c:MASCARA_V03_DESCARTAVEIS ()
  (mcad3:preview "DESCARTAVEL" "ELEMENTOS DISPENSAVEIS" 447)
)

(defun c:MASCARA_V03_REVISAR ()
  (mcad3:preview "REF_REVISAR" "OBJETOS A REVISAR" 47)
)

(defun c:MASCARA_V03_LIMPAR ()
  (sssetfirst nil nil)
  (redraw)
  (prompt "\nSelecao temporaria removida. O desenho nao foi modificado.")
  (princ)
)

;;; Alias seguro para quem carregou a primeira revisao da v0.3.
(defun c:MASCARA_PREVISUALIZAR ()
  (c:MASCARA_V03_DESCARTAVEIS)
)

;;; --------------------------------------------------------------------------
;;; Revisao 0.3.2 - agrupamento visual sem ler o interior de CINZA PONTOS.
;;; --------------------------------------------------------------------------

(vl-load-com)

(defun mcad32:detail-handles ()
  '("21BDF4A" "21BDF19" "21BDF16" "21BDF13" "21BDF10" "21BDF0D"
    "21BDF0A" "21BDF07" "21BDF04" "21BDF01" "21BDEFE" "21BDEFB"
    "21BDEF8" "21BDEF5" "21BDECC" "21BDECB" "21BDECA" "21BDEC9"
    "21BDEC8" "21BDEC7" "21BDEC6" "21BDEC5" "21BDEC3" "21BDEC2"
    "21BDEC1" "21BDEC0" "21BDEBE" "21BDE7E" "21BDE7D" "21BDE7B"
    "21BDE78" "21BDE75" "21BDE72" "21BDE6F" "21BDAD5" "21BDAD4"
    "21BDA6B" "21BDA6A" "21BDA4D" "21BDA4C" "21BDA1D" "21BDA1C"
    "21BD9F3" "21BD9F2" "21BC8DA" "21BBE5C" "219570D"
    "21BDAD2" "21BDAD1")
)

(defun mcad32:test-file-p ()
  (wcmatch (strcase (getvar "DWGNAME")) "TESTE_01_ARQUITETURA_ORIGINAL*.DWG")
)

(defun mcad32:confirmed-detail-p (data / handle)
  (setq handle (cdr (assoc 5 data)))
  (and (mcad32:test-file-p)
       handle
       (member (strcase handle) (mcad32:detail-handles)))
)

(defun mcad32:classification
  (data / entity-type layer-name block-name type-upper layer-upper block-upper)

  (setq entity-type (cdr (assoc 0 data)))
  (setq layer-name (cdr (assoc 8 data)))
  (if (null entity-type) (setq entity-type ""))
  (if (null layer-name) (setq layer-name ""))
  (setq block-name (mcad3:block-name data entity-type))
  (setq type-upper (strcase entity-type))
  (setq layer-upper (strcase layer-name))
  (setq block-upper (strcase block-name))

  (cond
    ((mcad32:confirmed-detail-p data)
      (list "DETALHE_AUXILIAR" "ALTA" "DESCARTAR_NA_COPIA"
            "Vista, marcador ou detalhe dispensavel validado neste arquivo"))

    ((= block-upper "CINZA PONTOS")
      (list "REF_ARQ_BASE" "ALTA" "MANTER_INTACTO"
            "Base arquitetonica mantida como bloco unico; interior nao analisado"))

    ((= block-upper "RFWERF")
      (list "REF_ARQ_MOBILIARIO" "ALTA" "MANTER_CONTEXTO"
            "Cama e criados-mudos confirmados visualmente"))

    ((= block-upper "BASE 1")
      (list "REF_ARQ_BASE" "MEDIA" "MANTER_INTACTO"
            "Bloco de base preservado no fluxo manual"))

    ((or (= type-upper "DIMENSION")
         (= type-upper "LEADER")
         (and (= layer-upper "CHAMADA - COTAS") (= type-upper "LINE"))
         (= layer-upper "TABELA")
         (= layer-upper "DEFPOINTS")
         (= block-upper "LOGO ICON")
         (= block-upper "NOME DO_DESENHO"))
      (list "DESCARTAVEL" "ALTA" "DESCARTAR_NA_COPIA"
            "Cota, chamada, tabela ou auxiliar ja validado"))

    ((and (= layer-upper "COR 10")
          (or (= type-upper "TEXT") (= type-upper "MTEXT")))
      (list "REF_ELE_ANOTACAO" "MEDIA" "ASSOCIAR_A_ITEM"
            "Texto eletrico de corrente ou tensao validado visualmente"))

    ((and (mcad3:contains layer-upper "SIMBOLOGIA PONTOS")
          (not (mcad3:contains layer-upper "HIDR"))
          (= type-upper "INSERT"))
      (list "REF_ELE_ITEM_BASE" "MEDIA" "CLASSIFICAR_SUBTIPO"
            "Bloco usado como semente de um item eletrico"))

    ((and (mcad3:contains layer-upper "SIMBOLOGIA PONTOS")
          (not (mcad3:contains layer-upper "HIDR")))
      (list "REF_ELE_COMPONENTE" "MEDIA" "ASSOCIAR_A_ITEM"
            "Componente grafico de simbologia eletrica"))

    ((= block-upper "SIMBOLO REGISTRO")
      (list "REF_HID_ITEM_BASE" "ALTA" "CLASSIFICAR_SUBTIPO"
            "Registro usado como semente de um item hidraulico"))

    ((and (or (and (mcad3:contains layer-upper "SIMBOLOGIA PONTOS")
                   (mcad3:contains layer-upper "HIDR"))
              (and (mcad3:contains layer-upper "HID -")
                   (not (mcad3:contains layer-upper "ESGOTO"))))
          (= type-upper "INSERT"))
      (list "REF_HID_ITEM_BASE" "MEDIA" "CLASSIFICAR_SUBTIPO"
            "Bloco usado como semente de um item hidraulico"))

    ((or (and (mcad3:contains layer-upper "SIMBOLOGIA PONTOS")
              (mcad3:contains layer-upper "HIDR"))
         (and (mcad3:contains layer-upper "HID -")
              (not (mcad3:contains layer-upper "ESGOTO"))))
      (list "REF_HID_COMPONENTE" "MEDIA" "ASSOCIAR_A_ITEM"
            "Componente grafico de simbologia hidraulica"))

    ((mcad3:contains layer-upper "MOBILI")
      (list "REF_ARQ_MOBILIARIO" "ALTA" "MANTER_CONTEXTO"
            "Mobiliario ou louca util para interpretar a planta"))

    ((or (mcad3:contains layer-upper "PROJE")
         (mcad3:contains layer-upper "HACH")
         (mcad3:contains layer-upper "TEXT")
         (mcad3:contains layer-upper "CHAMADA - T")
         (= layer-upper "TX-3")
         (= type-upper "TEXT")
         (= type-upper "MTEXT")
         (= type-upper "HATCH"))
      (list "REF_ARQ_CONTEXTO" "MEDIA" "MANTER_CONTEXTO"
            "Texto, titulo ou geometria util para interpretar a planta"))

    (T
      (list "REF_REVISAR_FUTURO" "BAIXA" "REVISAR_EM_OUTRO_ARQUIVO"
            "Objeto sem regra geral; os casos deste teste ja foram validados"))
  )
)

(defun mcad32:text-content (data / result pair code value)
  (setq result "")
  (foreach pair data
    (setq code (car pair))
    (setq value (cdr pair))
    (if (or (= code 1) (= code 3))
      (setq result (strcat result value))
    )
  )
  result
)

(defun mcad32:fallback-box (data / point x y z)
  (setq point (cdr (assoc 10 data)))
  (if point
    (progn
      (setq x (mcad3:point-value point 0))
      (setq y (mcad3:point-value point 1))
      (setq z (mcad3:point-value point 2))
      (list x y z x y z x y z)
    )
    (list 0.0 0.0 0.0 0.0 0.0 0.0 0.0 0.0 0.0)
  )
)

(defun mcad32:box (entity data / object result minimum maximum min-list max-list)
  (setq object (vlax-ename->vla-object entity))
  (setq result
    (vl-catch-all-apply 'vla-GetBoundingBox (list object 'minimum 'maximum)))
  (if (vl-catch-all-error-p result)
    (mcad32:fallback-box data)
    (progn
      (setq min-list (vlax-safearray->list minimum))
      (setq max-list (vlax-safearray->list maximum))
      (list
        (nth 0 min-list) (nth 1 min-list) (nth 2 min-list)
        (nth 0 max-list) (nth 1 max-list) (nth 2 max-list)
        (/ (+ (nth 0 min-list) (nth 0 max-list)) 2.0)
        (/ (+ (nth 1 min-list) (nth 1 max-list)) 2.0)
        (/ (+ (nth 2 min-list) (nth 2 max-list)) 2.0))
    )
  )
)

(defun mcad32:box-gap (box-a box-b / dx dy)
  (setq dx
    (max 0.0
         (- (nth 0 box-a) (nth 3 box-b))
         (- (nth 0 box-b) (nth 3 box-a))))
  (setq dy
    (max 0.0
         (- (nth 1 box-a) (nth 4 box-b))
         (- (nth 1 box-b) (nth 4 box-a))))
  (sqrt (+ (* dx dx) (* dy dy)))
)

;;; Seed: (entity handle block item-id box discipline)
(defun mcad32:nearest-seed (box seeds / best best-distance seed distance)
  (setq best nil best-distance nil)
  (foreach seed seeds
    (setq distance (mcad32:box-gap box (nth 4 seed)))
    (if (or (null best-distance) (< distance best-distance))
      (setq best seed best-distance distance)
    )
  )
  (if best (list best best-distance) nil)
)

(defun mcad32:association-confidence (distance annotation-p)
  (if annotation-p
    (cond ((<= distance 25.0) "ALTA")
          ((<= distance 100.0) "MEDIA")
          (T "BAIXA"))
    (cond ((<= distance 10.0) "ALTA")
          ((<= distance 50.0) "MEDIA")
          (T "BAIXA"))
  )
)

;;; Estado: (seed-handle item-id disciplina bloco cx cy componentes anotacoes
;;;          textos alta media baixa)
(defun mcad32:update-state
  (states seed text confidence / handle old new content separator)
  (setq handle (nth 1 seed))
  (setq old (assoc handle states))
  (if old
    (progn
      (setq content (nth 8 old))
      (if (and text (/= text ""))
        (progn
          (setq separator (if (= content "") "" " | "))
          (setq content (strcat content separator text))
        )
      )
      (setq new
        (list
          (nth 0 old) (nth 1 old) (nth 2 old) (nth 3 old)
          (nth 4 old) (nth 5 old)
          (1+ (nth 6 old))
          (+ (nth 7 old) (if (and text (/= text "")) 1 0))
          content
          (+ (nth 9 old) (if (= confidence "ALTA") 1 0))
          (+ (nth 10 old) (if (= confidence "MEDIA") 1 0))
          (+ (nth 11 old) (if (= confidence "BAIXA") 1 0))))
      (subst new old states)
    )
    states
  )
)

(defun mcad32:add-catalog
  (key handle catalog / found new)
  (if (setq found (assoc key catalog))
    (progn
      (setq new (list key (1+ (nth 1 found)) (nth 2 found)))
      (subst new found catalog))
    (cons (list key 1 handle) catalog)
  )
)

(defun mcad32:select-category (category expected title / source result index entity data)
  (setq source (ssget "_X" '((410 . "Model"))))
  (setq result (ssadd))
  (if source
    (progn
      (setq index 0)
      (while (< index (sslength source))
        (setq entity (ssname source index))
        (setq data (entget entity))
        (if (= (nth 0 (mcad32:classification data)) category)
          (ssadd entity result))
        (setq index (1+ index)))))
  (sssetfirst nil nil)
  (if (> (sslength result) 0) (sssetfirst nil result))
  (prompt (strcat "\n" title ": " (itoa (sslength result)) " objetos."))
  (if (= (sslength result) expected)
    (prompt " Contagem esperada confirmada.")
    (prompt (strcat " ATENCAO: eram esperados " (itoa expected) ".")))
  (prompt "\nSelecao temporaria; o desenho nao foi modificado.")
  (princ)
)

(defun c:MASCARA_V032_TEXTOS_ELETRICOS ()
  (mcad32:select-category "REF_ELE_ANOTACAO" 72 "TEXTOS ELETRICOS")
)

(defun c:MASCARA_V032_DETALHES ()
  (mcad32:select-category "DETALHE_AUXILIAR" 49 "DETALHES AUXILIARES")
)

(defun c:MASCARA_V032_SELECIONAR_BLOCO
  (/ wanted source result index entity data block-name)
  (setq wanted (getstring T "\nDigite ou cole o nome exato do bloco: "))
  (setq source (ssget "_X" '((0 . "INSERT") (410 . "Model"))))
  (setq result (ssadd))
  (if source
    (progn
      (setq index 0)
      (while (< index (sslength source))
        (setq entity (ssname source index))
        (setq data (entget entity))
        (setq block-name (cdr (assoc 2 data)))
        (if (and block-name (= (strcase block-name) (strcase wanted)))
          (ssadd entity result))
        (setq index (1+ index)))))
  (sssetfirst nil nil)
  (if (> (sslength result) 0) (sssetfirst nil result))
  (prompt (strcat "\nBloco " wanted ": " (itoa (sslength result)) " ocorrencias."))
  (prompt "\nSelecao temporaria; o desenho nao foi modificado.")
  (princ)
)

(defun c:MASCARA_V032_LIMPAR ()
  (c:MASCARA_V03_LIMPAR)
)

(defun c:MASCARA_V032_ANALISAR
  (/ *error* old-error component-file item-file catalog-file output-file
     drawing-path drawing-name prefix directory source index entity data
     classification category entity-type layer-name block-name handle box
     text-content electric-seeds hydraulic-seeds seed-count-e seed-count-h
     seed item-id states catalog nearest seed-match distance assoc-confidence
     associated-id associated-handle component-handle count-components
     count-associated count-low state entry key component-path item-path
     catalog-path annotation-p)

  (setq old-error *error*)
  (defun *error* (message)
    (if component-file (progn (close component-file) (setq component-file nil)))
    (if item-file (progn (close item-file) (setq item-file nil)))
    (if catalog-file (progn (close catalog-file) (setq catalog-file nil)))
    (setq *error* old-error)
    (if (and message
             (/= message "Function cancelled")
             (/= message "quit / exit abort"))
      (prompt (strcat "\nErro em MASCARA_V032_ANALISAR: " message)))
    (princ))

  (setq drawing-path (getvar "DWGPREFIX"))
  (setq drawing-name (getvar "DWGNAME"))
  (if (= drawing-path "")
    (prompt "\nSalve uma copia do DWG antes de executar a analise.")
    (progn
      (setq output-file
        (getfiled
          "Escolha a pasta para os mapas v0.3.2"
          (strcat drawing-path (mcad3:base-name drawing-name) "_componentes_v032.csv")
          "csv" 1))
      (if output-file
        (progn
          (setq directory (vl-filename-directory output-file))
          (setq prefix (strcat directory "\\" (mcad3:base-name drawing-name)))
          (setq component-path (strcat prefix "_componentes_v032.csv"))
          (setq item-path (strcat prefix "_itens_instalacoes_v032.csv"))
          (setq catalog-path (strcat prefix "_catalogo_blocos_v032.csv"))
          (setq source (ssget "_X" '((410 . "Model"))))
          (setq electric-seeds nil hydraulic-seeds nil states nil catalog nil)
          (setq seed-count-e 0 seed-count-h 0)

          ;; Primeiro passe: blocos-semente e centros visuais.
          (if source
            (progn
              (setq index 0)
              (while (< index (sslength source))
                (setq entity (ssname source index))
                (setq data (entget entity))
                (setq classification (mcad32:classification data))
                (setq category (nth 0 classification))
                (if (or (= category "REF_ELE_ITEM_BASE")
                        (= category "REF_HID_ITEM_BASE"))
                  (progn
                    (setq handle (cdr (assoc 5 data)))
                    (setq block-name (mcad3:block-name data "INSERT"))
                    (setq box (mcad32:box entity data))
                    (if (= category "REF_ELE_ITEM_BASE")
                      (progn
                        (setq seed-count-e (1+ seed-count-e))
                        (setq item-id
                          (strcat "ELE-" (mcad3:pad-number seed-count-e 4)))
                        (setq seed
                          (list entity handle block-name item-id box "ELETRICO"))
                        (setq electric-seeds (cons seed electric-seeds)))
                      (progn
                        (setq seed-count-h (1+ seed-count-h))
                        (setq item-id
                          (strcat "HID-" (mcad3:pad-number seed-count-h 4)))
                        (setq seed
                          (list entity handle block-name item-id box "HIDRAULICO"))
                        (setq hydraulic-seeds (cons seed hydraulic-seeds))))
                    (setq states
                      (cons
                        (list handle item-id (nth 5 seed) block-name
                              (nth 6 box) (nth 7 box) 1 0 "" 1 0 0)
                        states))
                    (setq catalog
                      (mcad32:add-catalog
                        (list (nth 5 seed) block-name) handle catalog)))
                )
                (setq index (1+ index)))))

          (setq component-file (open component-path "w"))
          (setq item-file (open item-path "w"))
          (setq catalog-file (open catalog-path "w"))

          (if (and component-file item-file catalog-file)
            (progn
              (write-line
                "\"ARQUIVO\";\"HANDLE\";\"LAYER_ORIGINAL\";\"TIPO_OBJETO\";\"NOME_BLOCO\";\"CONTEUDO_TEXTO\";\"CATEGORIA\";\"CENTRO_VISUAL_X\";\"CENTRO_VISUAL_Y\";\"MIN_X\";\"MIN_Y\";\"MAX_X\";\"MAX_Y\";\"ITEM_ASSOCIADO\";\"SEED_HANDLE\";\"DISTANCIA_VISUAL\";\"CONFIANCA_ASSOCIACAO\""
                component-file)
              (setq count-components 0 count-associated 0 count-low 0)

              ;; Segundo passe: componentes, textos e associacoes.
              (if source
                (progn
                  (setq index 0)
                  (while (< index (sslength source))
                    (setq entity (ssname source index))
                    (setq data (entget entity))
                    (setq classification (mcad32:classification data))
                    (setq category (nth 0 classification))
                    (setq entity-type (cdr (assoc 0 data)))
                    (setq layer-name (cdr (assoc 8 data)))
                    (setq handle (cdr (assoc 5 data)))
                    (if (null entity-type) (setq entity-type ""))
                    (if (null layer-name) (setq layer-name ""))
                    (if (null handle) (setq handle ""))
                    (setq block-name (mcad3:block-name data entity-type))
                    (setq text-content
                      (if (or (= entity-type "TEXT") (= entity-type "MTEXT"))
                        (mcad32:text-content data) ""))
                    (setq box (mcad32:box entity data))
                    (setq nearest nil associated-id "" associated-handle "")
                    (setq distance nil assoc-confidence "")

                    (cond
                      ((or (= category "REF_ELE_COMPONENTE")
                           (= category "REF_ELE_ANOTACAO"))
                        (setq nearest (mcad32:nearest-seed box electric-seeds)))
                      ((= category "REF_HID_COMPONENTE")
                        (setq nearest (mcad32:nearest-seed box hydraulic-seeds)))
                      ((or (= category "REF_ELE_ITEM_BASE")
                           (= category "REF_HID_ITEM_BASE"))
                        (setq seed
                          (if (= category "REF_ELE_ITEM_BASE")
                            (car (vl-remove-if-not
                              '(lambda (x) (= (nth 1 x) handle)) electric-seeds))
                            (car (vl-remove-if-not
                              '(lambda (x) (= (nth 1 x) handle)) hydraulic-seeds))))
                        (if seed
                          (progn
                            (setq associated-id (nth 3 seed))
                            (setq associated-handle (nth 1 seed))
                            (setq distance 0.0 assoc-confidence "ALTA"))))
                    )

                    (if nearest
                      (progn
                        (setq seed-match (nth 0 nearest))
                        (setq distance (nth 1 nearest))
                        (setq annotation-p (= category "REF_ELE_ANOTACAO"))
                        (setq assoc-confidence
                          (mcad32:association-confidence distance annotation-p))
                        (setq associated-id (nth 3 seed-match))
                        (setq associated-handle (nth 1 seed-match))
                        (setq states
                          (mcad32:update-state
                            states seed-match text-content assoc-confidence))
                        (setq count-associated (1+ count-associated))
                        (if (= assoc-confidence "BAIXA")
                          (setq count-low (1+ count-low)))))

                    (write-line
                      (strcat
                        (mcad3:csv-field drawing-name) ";"
                        (mcad3:csv-field handle) ";"
                        (mcad3:csv-field layer-name) ";"
                        (mcad3:csv-field entity-type) ";"
                        (mcad3:csv-field block-name) ";"
                        (mcad3:csv-field text-content) ";"
                        (mcad3:csv-field category) ";"
                        (mcad3:csv-field (mcad3:number-text (nth 6 box))) ";"
                        (mcad3:csv-field (mcad3:number-text (nth 7 box))) ";"
                        (mcad3:csv-field (mcad3:number-text (nth 0 box))) ";"
                        (mcad3:csv-field (mcad3:number-text (nth 1 box))) ";"
                        (mcad3:csv-field (mcad3:number-text (nth 3 box))) ";"
                        (mcad3:csv-field (mcad3:number-text (nth 4 box))) ";"
                        (mcad3:csv-field associated-id) ";"
                        (mcad3:csv-field associated-handle) ";"
                        (mcad3:csv-field (if distance (mcad3:number-text distance) "")) ";"
                        (mcad3:csv-field assoc-confidence))
                      component-file)
                    (setq count-components (1+ count-components))
                    (setq index (1+ index)))))

              (write-line
                "\"ARQUIVO\";\"ITEM_ID\";\"DISCIPLINA\";\"SEED_HANDLE\";\"NOME_BLOCO\";\"CENTRO_VISUAL_X\";\"CENTRO_VISUAL_Y\";\"QTD_COMPONENTES\";\"QTD_ANOTACOES\";\"ANOTACOES\";\"ASSOCIACOES_ALTAS\";\"ASSOCIACOES_MEDIAS\";\"ASSOCIACOES_BAIXAS\";\"SUBTIPO\""
                item-file)
              (foreach state states
                (write-line
                  (strcat
                    (mcad3:csv-field drawing-name) ";"
                    (mcad3:csv-field (nth 1 state)) ";"
                    (mcad3:csv-field (nth 2 state)) ";"
                    (mcad3:csv-field (nth 0 state)) ";"
                    (mcad3:csv-field (nth 3 state)) ";"
                    (mcad3:csv-field (mcad3:number-text (nth 4 state))) ";"
                    (mcad3:csv-field (mcad3:number-text (nth 5 state))) ";"
                    (mcad3:csv-field (itoa (nth 6 state))) ";"
                    (mcad3:csv-field (itoa (nth 7 state))) ";"
                    (mcad3:csv-field (nth 8 state)) ";"
                    (mcad3:csv-field (itoa (nth 9 state))) ";"
                    (mcad3:csv-field (itoa (nth 10 state))) ";"
                    (mcad3:csv-field (itoa (nth 11 state))) ";"
                    (mcad3:csv-field "A_CLASSIFICAR"))
                  item-file))

              (write-line
                "\"ARQUIVO\";\"DISCIPLINA\";\"NOME_BLOCO\";\"QUANTIDADE\";\"HANDLE_EXEMPLO\""
                catalog-file)
              (foreach entry catalog
                (setq key (nth 0 entry))
                (write-line
                  (strcat
                    (mcad3:csv-field drawing-name) ";"
                    (mcad3:csv-field (nth 0 key)) ";"
                    (mcad3:csv-field (nth 1 key)) ";"
                    (mcad3:csv-field (itoa (nth 1 entry))) ";"
                    (mcad3:csv-field (nth 2 entry)))
                  catalog-file))

              (close component-file) (setq component-file nil)
              (close item-file) (setq item-file nil)
              (close catalog-file) (setq catalog-file nil)

              (prompt (strcat "\nAnalise v0.3.2 concluida: "
                              (itoa count-components) " componentes."))
              (prompt (strcat "\nItens-base: ELE=" (itoa seed-count-e)
                              " | HID=" (itoa seed-count-h)
                              " | TOTAL=" (itoa (+ seed-count-e seed-count-h))))
              (prompt (strcat "\nComponentes associados automaticamente: "
                              (itoa count-associated)
                              " | associacoes de baixa confianca: "
                              (itoa count-low)))
              (prompt "\nCINZA PONTOS foi mantido intacto e nao teve seu interior lido.")
              (prompt (strcat "\nComponentes: " component-path))
              (prompt (strcat "\nItens: " item-path))
              (prompt (strcat "\nCatalogo: " catalog-path))
              (prompt "\nO desenho nao foi modificado e nenhuma layer foi criada."))
            (prompt "\nNao foi possivel criar os tres arquivos CSV."))
        )
        (prompt "\nOperacao cancelada. Nenhum arquivo foi criado."))))
  (setq *error* old-error)
  (princ)
)

;;; --------------------------------------------------------------------------
;;; Revisao 0.3.3 - legenda como fonte prioritaria e dicionario confirmado.
;;; --------------------------------------------------------------------------

;;; Entrada do dicionario:
;;; (BLOCO DISCIPLINA PAPEL CODIGO DESCRICAO ALTURA_CM LAYER_PROPOSTA
;;;  CONFIANCA FONTE)
(defun mcad33:dictionary ()
  '(
    ("FDF" "ELETRICO" "COMPONENTE_ASSOCIADO" "ELE_COMANDO_INTERRUPTOR"
      "Comando S do interruptor" ""
      "PONTOS_ELE_INTERRUPTORES" "ALTA" "CONFIRMACAO_USUARIO")
    ("EGT" "ELETRICO" "PONTO_PRINCIPAL" "ELE_TOMADA_MEDIA"
      "Tomada media" "A_CONFIRMAR_NA_LEGENDA"
      "PONTOS_ELE_TOMADA_MEDIA" "ALTA" "CONFIRMACAO_USUARIO")
    ("G4" "ELETRICO" "PONTO_PRINCIPAL" "ELE_TOMADA_BAIXA"
      "Tomada baixa" "30"
      "PONTOS_ELE_TOMADA_BAIXA" "ALTA" "LEGENDA_E_CONFIRMACAO_USUARIO")
    ("FGWSG" "ELETRICO" "PONTO_PRINCIPAL" "ELE_SOM_TETO"
      "Ponto de som no teto" "TETO"
      "PONTOS_ELE_SOM_TETO" "ALTA" "CONFIRMACAO_USUARIO")
    ("5T4" "ELETRICO" "PONTO_PRINCIPAL" "ELE_INTERNET_RJ45"
      "Ponto de internet RJ45" "245"
      "PONTOS_ELE_INTERNET_RJ45" "ALTA" "LEGENDA_E_CONFIRMACAO_USUARIO")
    ("DPALDAPDAE" "ELETRICO" "PONTO_PRINCIPAL"
      "ELE_TOMADA_DUPLA_CABECEIRA"
      "Conjunto de duas tomadas na cabeceira" "A_CONFIRMAR_POR_CONTEXTO"
      "PONTOS_ELE_TOMADA_DUPLA_CABECEIRA" "ALTA"
      "CONFIRMACAO_VISUAL")
    ("2TOAMDA" "ELETRICO" "PONTO_PRINCIPAL"
      "ELE_TOMADA_DUPLA_20A_220V"
      "Conjunto de duas tomadas 20A 220V" "A_CONFIRMAR_POR_CONTEXTO"
      "PONTOS_ELE_TOMADA_DUPLA_20A_220V" "ALTA"
      "CONFIRMACAO_VISUAL_E_ANOTACOES")
    ("FRZTG" "HIDRAULICO" "PONTO_PRINCIPAL" "HID_PIA_COZINHA"
      "Ponto para pia de cozinha" "A_CONFIRMAR_NA_LEGENDA"
      "PONTOS_HID_PIA_COZINHA" "ALTA" "CONFIRMACAO_USUARIO")
    ("RFR" "HIDRAULICO" "PONTO_PRINCIPAL" "HID_RALO_LINEAR"
      "Ralo linear de piso" "PISO"
      "PONTOS_HID_RALO_LINEAR" "ALTA" "LEGENDA_E_CONFIRMACAO_USUARIO")
    ("GH4EH4E" "HIDRAULICO" "PONTO_PRINCIPAL" "HID_DESVIADOR_CHUVEIRO"
      "Desviador de chuveiro" "110"
      "PONTOS_HID_DESVIADOR_CHUVEIRO" "ALTA"
      "LEGENDA_E_CONFIRMACAO_USUARIO")
    ("RZW" "HIDRAULICO" "PONTO_PRINCIPAL" "HID_MONOCOMANDO"
      "Ponto de monocomando" "A_CONFIRMAR_NA_LEGENDA"
      "PONTOS_HID_MONOCOMANDO" "MEDIA" "INTERPRETACAO_VISUAL")
    ("WSDEFC" "HIDRAULICO" "PONTO_PRINCIPAL" "HID_IRRIGACAO_JARDIM"
      "Ponto para irrigacao de jardim" "PLANTA"
      "PONTOS_HID_IRRIGACAO" "ALTA" "LEGENDA_E_INTERPRETACAO_VISUAL")
    ("SIMBOLO REGISTRO" "HIDRAULICO" "PONTO_PRINCIPAL"
      "HID_REGISTRO" "Registro hidraulico" ""
      "PONTOS_HID_REGISTROS" "ALTA" "NOME_DO_BLOCO")
  )
)

(defun mcad33:dict-find (block-name / wanted result entry)
  (setq wanted (strcase (if block-name block-name "")))
  (setq result nil)
  (foreach entry (mcad33:dictionary)
    (if (= wanted (strcase (nth 0 entry)))
      (setq result entry)
    )
  )
  result
)

(defun mcad33:block-name (entity data / object result)
  (setq result (mcad3:block-name data (cdr (assoc 0 data))))
  (if (= (cdr (assoc 0 data)) "INSERT")
    (progn
      (setq object (vlax-ename->vla-object entity))
      (if (vlax-property-available-p object 'EffectiveName)
        (setq result (vla-get-EffectiveName object))
      )
    )
  )
  result
)

(defun mcad33:fallback-info (data block-name / classification category)
  (setq classification (mcad32:classification data))
  (setq category (nth 0 classification))
  (cond
    ((= (strcase block-name) "CINZA PONTOS")
      (list block-name "ARQUITETURA" "BASE_INTACTA" "ARQ_BASE"
        "Base arquitetonica preservada; interior nao analisado" ""
        "REF_ARQ_BASE" "ALTA" "REGRA_V032"))
    ((and (mcad3:contains (strcase block-name) "HID -")
          (mcad3:contains (strcase block-name) "VISTA"))
      (list block-name "DETALHE" "DESCARTAVEL" "DETALHE_HID_VISTA"
        "Ponto pertencente somente as vistas auxiliares dispensaveis" ""
        "DESCARTAVEL" "ALTA" "REGRA_E_CONFIRMACAO_USUARIO"))
    ((= category "REF_ARQ_BASE")
      (list block-name "ARQUITETURA" "BASE_INTACTA" "ARQ_BASE"
        "Bloco de base arquitetonica preservado" ""
        "REF_ARQ_BASE" "ALTA" "REGRA_V032"))
    ((= category "REF_ARQ_MOBILIARIO")
      (list block-name "ARQUITETURA" "CONTEXTO" "ARQ_MOBILIARIO"
        "Mobiliario mantido como contexto visual" ""
        "REF_ARQ_MOBILIARIO" "ALTA" "REGRA_V032"))
    ((= category "DESCARTAVEL")
      (list block-name "DETALHE" "DESCARTAVEL" "DETALHE_DESCARTAVEL"
        "Logo, nome de desenho ou auxiliar dispensavel" ""
        "DESCARTAVEL" "ALTA" "REGRA_V032"))
    ((= category "REF_ELE_ITEM_BASE")
      (list block-name "ELETRICO" "PONTO_A_CLASSIFICAR" "SEM_MAPEAMENTO"
        "Bloco eletrico ainda sem correspondencia confirmada na legenda" ""
        "REF_ELE_REVISAR" "BAIXA" "REGRA_V032"))
    ((= category "REF_HID_ITEM_BASE")
      (list block-name "HIDRAULICO" "PONTO_A_CLASSIFICAR" "SEM_MAPEAMENTO"
        "Bloco hidraulico ainda sem correspondencia confirmada na legenda" ""
        "REF_HID_REVISAR" "BAIXA" "REGRA_V032"))
    ((= category "DETALHE_AUXILIAR")
      (list block-name "DETALHE" "DESCARTAVEL" "DETALHE_AUXILIAR"
        "Vista, marcador ou detalhe validado como dispensavel" ""
        "DESCARTAVEL" "ALTA" "REGRA_V032"))
    (T
      (list block-name "OUTRO" "A_CLASSIFICAR" "SEM_MAPEAMENTO"
        "Bloco ainda sem correspondencia confirmada na legenda" ""
        "REF_REVISAR" "BAIXA" "REGRA_V032"))
  )
)

(defun mcad33:block-info (data block-name / confirmed)
  (setq confirmed (mcad33:dict-find block-name))
  (if confirmed
    confirmed
    (mcad33:fallback-info data block-name)
  )
)

;;; Catalogo: (CHAVE NOME_BLOCO QUANTIDADE INFO)
(defun mcad33:add-catalog (catalog block-name info / key found updated)
  (setq key (strcat (nth 1 info) "|" (strcase block-name)))
  (if (setq found (assoc key catalog))
    (progn
      (setq updated (list key (nth 1 found) (1+ (nth 2 found)) (nth 3 found)))
      (subst updated found catalog)
    )
    (cons (list key block-name 1 info) catalog)
  )
)

(defun mcad33:color-text (data / color)
  (setq color (cdr (assoc 62 data)))
  (if color (itoa color) "BYLAYER")
)

(defun c:MASCARA_V033_ANALISAR
  (/ *error* old-error map-file catalog-file output-file drawing-path
     drawing-name directory prefix map-path catalog-path source index entity
     data handle layer-name block-name info point rotation scale-x scale-y box
     catalog total mapped rule-classified unmapped role code entry)

  (setq old-error *error*)
  (defun *error* (message)
    (if map-file (progn (close map-file) (setq map-file nil)))
    (if catalog-file (progn (close catalog-file) (setq catalog-file nil)))
    (setq *error* old-error)
    (if (and message
             (/= message "Function cancelled")
             (/= message "quit / exit abort"))
      (prompt (strcat "\nErro em MASCARA_V033_ANALISAR: " message)))
    (princ))

  (setq drawing-path (getvar "DWGPREFIX"))
  (setq drawing-name (getvar "DWGNAME"))
  (if (= drawing-path "")
    (prompt "\nSalve uma copia do DWG antes de executar a analise.")
    (progn
      (setq output-file
        (getfiled
          "Escolha a pasta para os mapas v0.3.3"
          (strcat drawing-path
                  (mcad3:base-name drawing-name)
                  "_itens_semanticos_v033.csv")
          "csv" 1))
      (if output-file
        (progn
          (setq directory (vl-filename-directory output-file))
          (setq prefix (strcat directory "\\" (mcad3:base-name drawing-name)))
          (setq map-path (strcat prefix "_itens_semanticos_v033.csv"))
          (setq catalog-path (strcat prefix "_catalogo_semantico_v033.csv"))
          (setq map-file (open map-path "w"))
          (setq catalog-file (open catalog-path "w"))
          (setq source (ssget "_X" '((0 . "INSERT") (410 . "Model"))))
          (setq catalog nil total 0 mapped 0 unmapped 0 rule-classified 0)

          (if (and map-file catalog-file)
            (progn
              (write-line
                "\"ARQUIVO\";\"HANDLE\";\"LAYER_ORIGINAL\";\"NOME_BLOCO\";\"DISCIPLINA\";\"PAPEL\";\"CODIGO_SEMANTICO\";\"DESCRICAO\";\"ALTURA_CM\";\"LAYER_PROPOSTA\";\"CONFIANCA\";\"FONTE\";\"CENTRO_X\";\"CENTRO_Y\";\"ROTACAO_GRAUS\";\"ESCALA_X\";\"ESCALA_Y\""
                map-file)

              (if source
                (progn
                  (setq index 0)
                  (while (< index (sslength source))
                    (setq entity (ssname source index))
                    (setq data (entget entity))
                    (setq handle (cdr (assoc 5 data)))
                    (setq layer-name (cdr (assoc 8 data)))
                    (setq block-name (mcad33:block-name entity data))
                    (if (null handle) (setq handle ""))
                    (if (null layer-name) (setq layer-name ""))
                    (setq info (mcad33:block-info data block-name))
                    (setq role (nth 2 info))
                    (setq code (nth 3 info))
                    (setq box (mcad32:box entity data))
                    (setq rotation (cdr (assoc 50 data)))
                    (setq scale-x (cdr (assoc 41 data)))
                    (setq scale-y (cdr (assoc 42 data)))
                    (if (null rotation) (setq rotation 0.0))
                    (if (null scale-x) (setq scale-x 1.0))
                    (if (null scale-y) (setq scale-y 1.0))

                    (write-line
                      (strcat
                        (mcad3:csv-field drawing-name) ";"
                        (mcad3:csv-field handle) ";"
                        (mcad3:csv-field layer-name) ";"
                        (mcad3:csv-field block-name) ";"
                        (mcad3:csv-field (nth 1 info)) ";"
                        (mcad3:csv-field role) ";"
                        (mcad3:csv-field code) ";"
                        (mcad3:csv-field (nth 4 info)) ";"
                        (mcad3:csv-field (nth 5 info)) ";"
                        (mcad3:csv-field (nth 6 info)) ";"
                        (mcad3:csv-field (nth 7 info)) ";"
                        (mcad3:csv-field (nth 8 info)) ";"
                        (mcad3:csv-field (mcad3:number-text (nth 6 box))) ";"
                        (mcad3:csv-field (mcad3:number-text (nth 7 box))) ";"
                        (mcad3:csv-field
                          (mcad3:number-text (* rotation (/ 180.0 pi)))) ";"
                        (mcad3:csv-field (mcad3:number-text scale-x)) ";"
                        (mcad3:csv-field (mcad3:number-text scale-y)))
                      map-file)

                    (setq catalog
                      (mcad33:add-catalog catalog block-name info))
                    (setq total (1+ total))
                    (cond
                      ((mcad33:dict-find block-name)
                        (setq mapped (1+ mapped)))
                      ((= code "SEM_MAPEAMENTO")
                        (setq unmapped (1+ unmapped)))
                      (T
                        (setq rule-classified (1+ rule-classified))))
                    (setq index (1+ index)))))

              (write-line
                "\"ARQUIVO\";\"DISCIPLINA\";\"NOME_BLOCO\";\"QUANTIDADE\";\"PAPEL\";\"CODIGO_SEMANTICO\";\"DESCRICAO\";\"ALTURA_CM\";\"LAYER_PROPOSTA\";\"CONFIANCA\";\"FONTE\""
                catalog-file)
              (foreach entry catalog
                (setq info (nth 3 entry))
                (write-line
                  (strcat
                    (mcad3:csv-field drawing-name) ";"
                    (mcad3:csv-field (nth 1 info)) ";"
                    (mcad3:csv-field (nth 1 entry)) ";"
                    (mcad3:csv-field (itoa (nth 2 entry))) ";"
                    (mcad3:csv-field (nth 2 info)) ";"
                    (mcad3:csv-field (nth 3 info)) ";"
                    (mcad3:csv-field (nth 4 info)) ";"
                    (mcad3:csv-field (nth 5 info)) ";"
                    (mcad3:csv-field (nth 6 info)) ";"
                    (mcad3:csv-field (nth 7 info)) ";"
                    (mcad3:csv-field (nth 8 info)))
                  catalog-file))

              (close map-file) (setq map-file nil)
              (close catalog-file) (setq catalog-file nil)
              (prompt (strcat "\nAnalise v0.3.3 concluida: "
                              (itoa total) " blocos de topo analisados."))
              (prompt (strcat "\nMapeados pelo dicionario/legenda: "
                              (itoa mapped)
                              " | classificados por regra: "
                              (itoa rule-classified)
                              " | ainda sem mapeamento: "
                              (itoa unmapped)))
              (prompt "\nCINZA PONTOS permaneceu intacto; seu interior nao foi lido.")
              (prompt (strcat "\nItens semanticos: " map-path))
              (prompt (strcat "\nCatalogo semantico: " catalog-path))
              (prompt "\nO desenho nao foi modificado e nenhuma layer foi criada."))
            (prompt "\nNao foi possivel criar os arquivos v0.3.3.")))
        (prompt "\nOperacao cancelada. Nenhum arquivo foi criado."))))
  (setq *error* old-error)
  (princ)
)

;;; Item de texto da legenda: (HANDLE CONTEUDO BOX)
;;; Item de bloco da legenda: (HANDLE NOME_BLOCO BOX)
(defun mcad33:find-row-text
  (block-item texts / block-box best best-score text-item text-box
     text-height dx dy score)
  (setq block-box (nth 2 block-item))
  (setq best nil best-score nil)
  (foreach text-item texts
    (setq text-box (nth 2 text-item))
    (setq text-height (max 0.001 (- (nth 4 text-box) (nth 1 text-box))))
    (setq dx (- (nth 0 text-box) (nth 3 block-box)))
    (setq dy (abs (- (nth 7 text-box) (nth 7 block-box))))
    (if (and (>= dx (* -2.0 text-height))
             (<= dx (* 60.0 text-height))
             (<= dy (* 1.75 text-height)))
      (progn
        (setq score (+ (* 20.0 dy) (max 0.0 dx)))
        (if (or (null best-score) (< score best-score))
          (setq best text-item best-score score)))))
  (if best
    (progn
      (setq text-box (nth 2 best))
      (setq text-height (max 0.001 (- (nth 4 text-box) (nth 1 text-box))))
      (setq dx (- (nth 0 text-box) (nth 3 block-box)))
      (setq dy (abs (- (nth 7 text-box) (nth 7 block-box))))
      (list best dx dy
        (if (and (<= dy (* 0.65 text-height))
                 (<= dx (* 25.0 text-height)))
          "ALTA" "MEDIA")))
    nil)
)

(defun c:MASCARA_V033_LER_LEGENDA
  (/ *error* old-error raw-file pair-file selection output-file drawing-path
     drawing-name directory prefix raw-path pair-path index entity data
     entity-type handle layer-name block-name text-content box texts blocks
     text-item block-item match matched-text dx dy confidence raw-count
     pair-count)

  (setq old-error *error*)
  (defun *error* (message)
    (if raw-file (progn (close raw-file) (setq raw-file nil)))
    (if pair-file (progn (close pair-file) (setq pair-file nil)))
    (setq *error* old-error)
    (if (and message
             (/= message "Function cancelled")
             (/= message "quit / exit abort"))
      (prompt (strcat "\nErro em MASCARA_V033_LER_LEGENDA: " message)))
    (princ))

  (prompt
    "\nSelecione somente os quadros de simbologia eletrica e hidraulica.")
  (setq selection (ssget '((410 . "Model"))))
  (if selection
    (progn
      (setq drawing-path (getvar "DWGPREFIX"))
      (setq drawing-name (getvar "DWGNAME"))
      (setq output-file
        (getfiled
          "Escolha a pasta para a leitura da legenda v0.3.3"
          (strcat drawing-path
                  (mcad3:base-name drawing-name)
                  "_legenda_objetos_v033.csv")
          "csv" 1))
      (if output-file
        (progn
          (setq directory (vl-filename-directory output-file))
          (setq prefix (strcat directory "\\" (mcad3:base-name drawing-name)))
          (setq raw-path (strcat prefix "_legenda_objetos_v033.csv"))
          (setq pair-path (strcat prefix "_legenda_pares_v033.csv"))
          (setq raw-file (open raw-path "w"))
          (setq pair-file (open pair-path "w"))
          (setq texts nil blocks nil raw-count 0 pair-count 0)

          (if (and raw-file pair-file)
            (progn
              (write-line
                "\"ARQUIVO\";\"HANDLE\";\"TIPO_OBJETO\";\"NOME_BLOCO\";\"CONTEUDO_TEXTO\";\"LAYER\";\"COR\";\"CENTRO_X\";\"CENTRO_Y\";\"MIN_X\";\"MIN_Y\";\"MAX_X\";\"MAX_Y\""
                raw-file)
              (setq index 0)
              (while (< index (sslength selection))
                (setq entity (ssname selection index))
                (setq data (entget entity))
                (setq entity-type (cdr (assoc 0 data)))
                (setq handle (cdr (assoc 5 data)))
                (setq layer-name (cdr (assoc 8 data)))
                (if (null entity-type) (setq entity-type ""))
                (if (null handle) (setq handle ""))
                (if (null layer-name) (setq layer-name ""))
                (setq block-name (mcad33:block-name entity data))
                (setq text-content
                  (if (or (= entity-type "TEXT")
                          (= entity-type "MTEXT")
                          (= entity-type "ATTRIB")
                          (= entity-type "ATTDEF"))
                    (mcad32:text-content data) ""))
                (setq box (mcad32:box entity data))

                (if (/= text-content "")
                  (setq texts (cons (list handle text-content box) texts)))
                (if (= entity-type "INSERT")
                  (setq blocks (cons (list handle block-name box) blocks)))

                (write-line
                  (strcat
                    (mcad3:csv-field drawing-name) ";"
                    (mcad3:csv-field handle) ";"
                    (mcad3:csv-field entity-type) ";"
                    (mcad3:csv-field block-name) ";"
                    (mcad3:csv-field text-content) ";"
                    (mcad3:csv-field layer-name) ";"
                    (mcad3:csv-field (mcad33:color-text data)) ";"
                    (mcad3:csv-field (mcad3:number-text (nth 6 box))) ";"
                    (mcad3:csv-field (mcad3:number-text (nth 7 box))) ";"
                    (mcad3:csv-field (mcad3:number-text (nth 0 box))) ";"
                    (mcad3:csv-field (mcad3:number-text (nth 1 box))) ";"
                    (mcad3:csv-field (mcad3:number-text (nth 3 box))) ";"
                    (mcad3:csv-field (mcad3:number-text (nth 4 box))))
                  raw-file)
                (setq raw-count (1+ raw-count))
                (setq index (1+ index)))

              (write-line
                "\"ARQUIVO\";\"HANDLE_SIMBOLO\";\"NOME_BLOCO\";\"HANDLE_TEXTO\";\"DESCRICAO_LEGENDA\";\"DISTANCIA_X\";\"DESALINHAMENTO_Y\";\"CONFIANCA_PAREAMENTO\""
                pair-file)
              (foreach block-item blocks
                (setq match (mcad33:find-row-text block-item texts))
                (if match
                  (progn
                    (setq matched-text (nth 0 match))
                    (setq dx (nth 1 match))
                    (setq dy (nth 2 match))
                    (setq confidence (nth 3 match))
                    (write-line
                      (strcat
                        (mcad3:csv-field drawing-name) ";"
                        (mcad3:csv-field (nth 0 block-item)) ";"
                        (mcad3:csv-field (nth 1 block-item)) ";"
                        (mcad3:csv-field (nth 0 matched-text)) ";"
                        (mcad3:csv-field (nth 1 matched-text)) ";"
                        (mcad3:csv-field (mcad3:number-text dx)) ";"
                        (mcad3:csv-field (mcad3:number-text dy)) ";"
                        (mcad3:csv-field confidence))
                      pair-file)
                    (setq pair-count (1+ pair-count)))))

              (close raw-file) (setq raw-file nil)
              (close pair-file) (setq pair-file nil)
              (prompt (strcat "\nLeitura da legenda concluida: "
                              (itoa raw-count) " objetos exportados."))
              (prompt (strcat "\nBlocos pareados automaticamente com textos: "
                              (itoa pair-count) "."))
              (prompt (strcat "\nObjetos da legenda: " raw-path))
              (prompt (strcat "\nPares simbolo/texto: " pair-path))
              (prompt "\nO desenho nao foi modificado."))
            (prompt "\nNao foi possivel criar os arquivos da legenda.")))
        (prompt "\nOperacao cancelada. Nenhum arquivo foi criado.")))
    (prompt "\nNenhum objeto foi selecionado. O desenho nao foi modificado."))
  (setq *error* old-error)
  (princ)
)

(defun c:MASCARA_V033_LIMPAR ()
  (c:MASCARA_V03_LIMPAR)
)

;;; --------------------------------------------------------------------------
;;; Revisao 0.3.4 - aprende a legenda no proprio DWG.
;;; - Exclui da contagem todos os INSERTs selecionados dentro da legenda.
;;; - Usa a chave DISCIPLINA + NOME_BLOCO para evitar colisoes eletrico/hidraulico.
;;; - Mantem como ambiguo um bloco ligado a mais de uma descricao na mesma
;;;   disciplina.
;;; --------------------------------------------------------------------------

(defun mcad34:discipline-from-data (data block-name / layer category)
  (setq layer (strcase (if (cdr (assoc 8 data)) (cdr (assoc 8 data)) "")))
  (setq category (nth 0 (mcad32:classification data)))
  (cond
    ((mcad3:contains layer "HIDR") "HIDRAULICO")
    ((and (mcad3:contains layer "SIMBOLOGIA PONTOS")
          (not (mcad3:contains layer "HIDR"))) "ELETRICO")
    ((= category "REF_HID_ITEM_BASE") "HIDRAULICO")
    ((= category "REF_ELE_ITEM_BASE") "ELETRICO")
    ((= (strcase block-name) "SIMBOLO REGISTRO") "HIDRAULICO")
    ((= (strcase block-name) "CINZA PONTOS") "ARQUITETURA")
    (T "OUTRO")
  )
)

(defun mcad34:legend-discipline
  (block-item electric-title-x hydraulic-title-x / layer center-x)
  (setq layer (strcase (if (nth 3 block-item) (nth 3 block-item) "")))
  (setq center-x (nth 6 (nth 2 block-item)))
  (cond
    ((mcad3:contains layer "HIDR") "HIDRAULICO")
    ((mcad3:contains layer "EL") "ELETRICO")
    ((and electric-title-x hydraulic-title-x)
      (if (< (abs (- center-x electric-title-x))
             (abs (- center-x hydraulic-title-x)))
        "ELETRICO" "HIDRAULICO"))
    (hydraulic-title-x "HIDRAULICO")
    (T "ELETRICO")
  )
)

;;; Mapa de legenda: (CHAVE DISCIPLINA NOME_BLOCO LISTA_DESCRICOES)
(defun mcad34:add-map
  (legend-map discipline block-name description / key found descriptions updated)
  (setq key (strcat discipline "|" (strcase block-name)))
  (if (setq found (assoc key legend-map))
    (progn
      (setq descriptions (nth 3 found))
      (if (not (member description descriptions))
        (setq descriptions (cons description descriptions)))
      (setq updated (list key discipline (nth 2 found) descriptions))
      (subst updated found legend-map))
    (cons (list key discipline block-name (list description)) legend-map)
  )
)

(defun mcad34:map-find (legend-map discipline block-name)
  (assoc (strcat discipline "|" (strcase block-name)) legend-map)
)

(defun mcad34:join (values separator / result value)
  (setq result "")
  (foreach value values
    (setq result
      (strcat result (if (= result "") "" separator) value))
  )
  result
)

(defun mcad34:slug (text / upper cut-position translated result index char)
  (setq upper (strcase (if text text "")))
  (if (setq cut-position (vl-string-search " (" upper))
    (setq upper (substr upper 1 cut-position)))
  (setq translated
    (vl-string-translate
      "ÁÀÂÃÉÊÍÓÔÕÚÜÇ"
      "AAAAEEIOOOUUC"
      upper))
  (setq result "" index 1)
  (while (<= index (strlen translated))
    (setq char (substr translated index 1))
    (cond
      ((wcmatch char "[A-Z0-9]")
        (setq result (strcat result char)))
      ((and (/= result "")
            (/= (substr result (strlen result) 1) "_"))
        (setq result (strcat result "_"))))
    (setq index (1+ index)))
  (if (and (> (strlen result) 0)
           (= (substr result (strlen result) 1) "_"))
    (setq result (substr result 1 (1- (strlen result)))))
  result
)

(defun mcad34:height-from-description
  (description / upper position tail end-position)
  (setq upper (strcase (if description description "")))
  (cond
    ((setq position (vl-string-search "H=" upper))
      (setq tail (substr upper (+ position 3)))
      (cond
        ((setq end-position (vl-string-search "CM" tail))
          (substr tail 1 end-position))
        ((setq end-position (vl-string-search ")" tail))
          (substr tail 1 end-position))
        (T tail)))
    ((or (mcad3:contains upper "JUNTO AO TETO")
         (mcad3:contains upper "NO TETO")
         (mcad3:contains upper "DE TETO")) "TETO")
    ((or (mcad3:contains upper "NO PISO")
         (mcad3:contains upper "DE PISO")
         (mcad3:contains upper "RALO")) "PISO")
    ((mcad3:contains upper "VER MANUAL") "VER_MANUAL")
    ((mcad3:contains upper "INDICADA NA PLANTA") "INDICADA_NA_PLANTA")
    (T "")
  )
)

(defun mcad34:role-from-description (description / upper)
  (setq upper (strcase (if description description "")))
  (if (or (mcad3:contains upper "VOLTAG")
          (mcad3:contains upper "AMPER"))
    "COMPONENTE_ASSOCIADO"
    "PONTO_PRINCIPAL")
)

(defun mcad34:info-from-map
  (map-entry / discipline block-name descriptions description code height)
  (setq discipline (nth 1 map-entry))
  (setq block-name (nth 2 map-entry))
  (setq descriptions (nth 3 map-entry))
  (if (= (length descriptions) 1)
    (progn
      (setq description (car descriptions))
      (setq code
        (strcat
          (if (= discipline "HIDRAULICO") "HID_" "ELE_")
          (mcad34:slug description)))
      (setq height (mcad34:height-from-description description))
      (list block-name discipline
        (mcad34:role-from-description description)
        code description height
        (strcat "PONTOS_" code)
        "ALTA" "LEGENDA_DWG"))
    (progn
      (setq description (mcad34:join (reverse descriptions) " | "))
      (list block-name discipline "PONTO_AMBIGUO"
        "AMBIGUO_LEGENDA" description ""
        "REF_REVISAR_AMBIGUO" "BAIXA" "LEGENDA_DWG_MULTIPLA"))
  )
)

(defun mcad35:near-room-text-p
  (entity data fragment maximum-distance / source index candidate
     candidate-data candidate-type candidate-layer content target-box
     candidate-box found)
  (setq source (ssget "_X" '((410 . "Model"))))
  (setq target-box (mcad32:box entity data))
  (setq found nil index 0)
  (if source
    (while (and (< index (sslength source)) (not found))
      (setq candidate (ssname source index))
      (setq candidate-data (entget candidate))
      (setq candidate-type (cdr (assoc 0 candidate-data)))
      (setq candidate-layer
        (strcase
          (if (cdr (assoc 8 candidate-data))
            (cdr (assoc 8 candidate-data)) "")))
      (if (and (or (= candidate-type "TEXT")
                   (= candidate-type "MTEXT"))
               (= candidate-layer "TX-3"))
        (progn
          (setq content (mcad32:text-content candidate-data))
          (if (mcad3:contains content fragment)
            (progn
              (setq candidate-box (mcad32:box candidate candidate-data))
              (if (<= (mcad32:box-gap target-box candidate-box)
                      maximum-distance)
                (setq found T))))))
      (setq index (1+ index))))
  found
)

(defun mcad35:resolve-context
  (entity data block-name info / descriptions)
  (if (and (= (nth 3 info) "AMBIGUO_LEGENDA")
           (= (strcase block-name) "GH4EH4E"))
    (cond
      ((mcad35:near-room-text-p entity data "BANHO" 350.0)
        (list block-name "HIDRAULICO" "PONTO_PRINCIPAL"
          "HID_DESVIADOR_CHUVEIRO"
          "Desviador de chuveiro" "110"
          "PONTOS_HID_DESVIADOR_CHUVEIRO" "ALTA"
          "CONTEXTO_AMBIENTE_E_CONFIRMACAO_USUARIO"))
      ((or (mcad35:near-room-text-p entity data "SERVI" 350.0)
           (mcad35:near-room-text-p entity data "LAVAND" 350.0))
        (list block-name "HIDRAULICO" "PONTO_PRINCIPAL"
          "HID_PONTO_TANQUINHO"
          "Ponto para tanquinho" ""
          "PONTOS_HID_TANQUINHO" "MEDIA"
          "CONTEXTO_AMBIENTE"))
      (T info))
    info
  )
)

(defun c:MASCARA_V035_ANALISAR
  (/ *error* old-error map-file catalog-file dictionary-file selection
     output-file drawing-path drawing-name directory prefix map-path
     catalog-path dictionary-path selection-index entity data entity-type
     handle layer-name block-name text-content box legend-handles texts blocks
     text-item block-item electric-title-x hydraulic-title-x match
     matched-text discipline legend-map map-entry info source index catalog
     total mapped ambiguous fallback unmapped entry descriptions rotation
     scale-x scale-y)

  (setq old-error *error*)
  (defun *error* (message)
    (if map-file (progn (close map-file) (setq map-file nil)))
    (if catalog-file (progn (close catalog-file) (setq catalog-file nil)))
    (if dictionary-file
      (progn (close dictionary-file) (setq dictionary-file nil)))
    (setq *error* old-error)
    (if (and message
             (/= message "Function cancelled")
             (/= message "quit / exit abort"))
      (prompt (strcat "\nErro em MASCARA_V035_ANALISAR: " message)))
    (princ))

  (prompt
    "\nSelecione novamente somente os dois quadros de simbologia.")
  (setq selection (ssget '((410 . "Model"))))
  (if selection
    (progn
      (setq drawing-path (getvar "DWGPREFIX"))
      (setq drawing-name (getvar "DWGNAME"))
      (setq output-file
        (getfiled
          "Escolha a pasta para os mapas v0.3.5"
          (strcat drawing-path
                  (mcad3:base-name drawing-name)
                  "_itens_semanticos_v035.csv")
          "csv" 1))
      (if output-file
        (progn
          (setq directory (vl-filename-directory output-file))
          (setq prefix (strcat directory "\\" (mcad3:base-name drawing-name)))
          (setq map-path (strcat prefix "_itens_semanticos_v035.csv"))
          (setq catalog-path (strcat prefix "_catalogo_semantico_v035.csv"))
          (setq dictionary-path
            (strcat prefix "_dicionario_legenda_v035.csv"))
          (setq map-file (open map-path "w"))
          (setq catalog-file (open catalog-path "w"))
          (setq dictionary-file (open dictionary-path "w"))
          (setq legend-handles nil texts nil blocks nil)
          (setq electric-title-x nil hydraulic-title-x nil)

          ;; Le a selecao da legenda e guarda os seus handles para exclusao.
          (setq selection-index 0)
          (while (< selection-index (sslength selection))
            (setq entity (ssname selection selection-index))
            (setq data (entget entity))
            (setq entity-type (cdr (assoc 0 data)))
            (setq handle (cdr (assoc 5 data)))
            (setq layer-name (cdr (assoc 8 data)))
            (if (null entity-type) (setq entity-type ""))
            (if (null handle) (setq handle ""))
            (if (null layer-name) (setq layer-name ""))
            (setq block-name (mcad33:block-name entity data))
            (setq text-content
              (if (or (= entity-type "TEXT")
                      (= entity-type "MTEXT")
                      (= entity-type "ATTRIB")
                      (= entity-type "ATTDEF"))
                (mcad32:text-content data) ""))
            (setq box (mcad32:box entity data))
            (setq legend-handles (cons handle legend-handles))
            (if (/= text-content "")
              (progn
                (setq text-item (list handle text-content box))
                (setq texts (cons text-item texts))
                (if (and (mcad3:contains text-content "SIMBOLOGIA")
                         (mcad3:contains text-content "HIDR"))
                  (setq hydraulic-title-x (nth 6 box)))
                (if (and (mcad3:contains text-content "SIMBOLOGIA")
                         (not (mcad3:contains text-content "HIDR")))
                  (setq electric-title-x (nth 6 box)))))
            (if (= entity-type "INSERT")
              (setq blocks
                (cons (list handle block-name box layer-name) blocks)))
            (setq selection-index (1+ selection-index)))

          ;; Monta o dicionario em memoria pelo alinhamento simbolo/texto.
          (setq legend-map nil)
          (foreach block-item blocks
            (setq match (mcad33:find-row-text block-item texts))
            (if match
              (progn
                (setq matched-text (nth 0 match))
                (setq discipline
                  (mcad34:legend-discipline
                    block-item electric-title-x hydraulic-title-x))
                (setq legend-map
                  (mcad34:add-map legend-map discipline (nth 1 block-item)
                    (nth 1 matched-text))))))

          (if (and map-file catalog-file dictionary-file)
            (progn
              (write-line
                "\"ARQUIVO\";\"DISCIPLINA\";\"NOME_BLOCO\";\"QTD_DESCRICOES\";\"DESCRICOES_LEGENDA\";\"STATUS\";\"CODIGO_SEMANTICO\";\"ALTURA_CM\";\"LAYER_PROPOSTA\""
                dictionary-file)
              (foreach entry legend-map
                (setq info (mcad34:info-from-map entry))
                (setq descriptions (nth 3 entry))
                (write-line
                  (strcat
                    (mcad3:csv-field drawing-name) ";"
                    (mcad3:csv-field (nth 1 entry)) ";"
                    (mcad3:csv-field (nth 2 entry)) ";"
                    (mcad3:csv-field (itoa (length descriptions))) ";"
                    (mcad3:csv-field
                      (mcad34:join (reverse descriptions) " | ")) ";"
                    (mcad3:csv-field
                      (if (= (length descriptions) 1)
                        "UNIVOCO" "AMBIGUO")) ";"
                    (mcad3:csv-field (nth 3 info)) ";"
                    (mcad3:csv-field (nth 5 info)) ";"
                    (mcad3:csv-field (nth 6 info)))
                  dictionary-file))

              (write-line
                "\"ARQUIVO\";\"HANDLE\";\"LAYER_ORIGINAL\";\"NOME_BLOCO\";\"DISCIPLINA\";\"PAPEL\";\"CODIGO_SEMANTICO\";\"DESCRICAO\";\"ALTURA_CM\";\"LAYER_PROPOSTA\";\"CONFIANCA\";\"FONTE\";\"CENTRO_X\";\"CENTRO_Y\";\"ROTACAO_GRAUS\";\"ESCALA_X\";\"ESCALA_Y\""
                map-file)

              (setq source (ssget "_X" '((0 . "INSERT") (410 . "Model"))))
              (setq catalog nil total 0 mapped 0 ambiguous 0 fallback 0
                    unmapped 0)
              (if source
                (progn
                  (setq index 0)
                  (while (< index (sslength source))
                    (setq entity (ssname source index))
                    (setq data (entget entity))
                    (setq handle (cdr (assoc 5 data)))
                    (if (null handle) (setq handle ""))
                    ;; INSERTs da propria legenda nao sao pontos da planta.
                    (if (not (member handle legend-handles))
                      (progn
                        (setq layer-name (cdr (assoc 8 data)))
                        (if (null layer-name) (setq layer-name ""))
                        (setq block-name (mcad33:block-name entity data))
                        (setq discipline
                          (mcad34:discipline-from-data data block-name))
                        (setq map-entry
                          (mcad34:map-find legend-map discipline block-name))
                        (if map-entry
                          (setq info (mcad34:info-from-map map-entry))
                          (setq info (mcad33:block-info data block-name)))
                        (setq info
                          (mcad35:resolve-context
                            entity data block-name info))
                        (setq box (mcad32:box entity data))
                        (setq rotation (cdr (assoc 50 data)))
                        (setq scale-x (cdr (assoc 41 data)))
                        (setq scale-y (cdr (assoc 42 data)))
                        (if (null rotation) (setq rotation 0.0))
                        (if (null scale-x) (setq scale-x 1.0))
                        (if (null scale-y) (setq scale-y 1.0))

                        (write-line
                          (strcat
                            (mcad3:csv-field drawing-name) ";"
                            (mcad3:csv-field handle) ";"
                            (mcad3:csv-field layer-name) ";"
                            (mcad3:csv-field block-name) ";"
                            (mcad3:csv-field (nth 1 info)) ";"
                            (mcad3:csv-field (nth 2 info)) ";"
                            (mcad3:csv-field (nth 3 info)) ";"
                            (mcad3:csv-field (nth 4 info)) ";"
                            (mcad3:csv-field (nth 5 info)) ";"
                            (mcad3:csv-field (nth 6 info)) ";"
                            (mcad3:csv-field (nth 7 info)) ";"
                            (mcad3:csv-field (nth 8 info)) ";"
                            (mcad3:csv-field
                              (mcad3:number-text (nth 6 box))) ";"
                            (mcad3:csv-field
                              (mcad3:number-text (nth 7 box))) ";"
                            (mcad3:csv-field
                              (mcad3:number-text
                                (* rotation (/ 180.0 pi)))) ";"
                            (mcad3:csv-field
                              (mcad3:number-text scale-x)) ";"
                            (mcad3:csv-field
                              (mcad3:number-text scale-y)))
                          map-file)

                        (setq catalog
                          (mcad33:add-catalog catalog block-name info))
                        (setq total (1+ total))
                        (cond
                          ((= (nth 3 info) "AMBIGUO_LEGENDA")
                            (setq ambiguous (1+ ambiguous)))
                          (map-entry
                            (setq mapped (1+ mapped)))
                          ((= (nth 3 info) "SEM_MAPEAMENTO")
                            (setq unmapped (1+ unmapped)))
                          (T
                            (setq fallback (1+ fallback))))))
                    (setq index (1+ index)))))

              (write-line
                "\"ARQUIVO\";\"DISCIPLINA\";\"NOME_BLOCO\";\"QUANTIDADE\";\"PAPEL\";\"CODIGO_SEMANTICO\";\"DESCRICAO\";\"ALTURA_CM\";\"LAYER_PROPOSTA\";\"CONFIANCA\";\"FONTE\""
                catalog-file)
              (foreach entry catalog
                (setq info (nth 3 entry))
                (write-line
                  (strcat
                    (mcad3:csv-field drawing-name) ";"
                    (mcad3:csv-field (nth 1 info)) ";"
                    (mcad3:csv-field (nth 1 entry)) ";"
                    (mcad3:csv-field (itoa (nth 2 entry))) ";"
                    (mcad3:csv-field (nth 2 info)) ";"
                    (mcad3:csv-field (nth 3 info)) ";"
                    (mcad3:csv-field (nth 4 info)) ";"
                    (mcad3:csv-field (nth 5 info)) ";"
                    (mcad3:csv-field (nth 6 info)) ";"
                    (mcad3:csv-field (nth 7 info)) ";"
                    (mcad3:csv-field (nth 8 info)))
                  catalog-file))

              (close map-file) (setq map-file nil)
              (close catalog-file) (setq catalog-file nil)
              (close dictionary-file) (setq dictionary-file nil)

              (prompt (strcat "\nAnalise v0.3.5 concluida: "
                              (itoa total)
                              " blocos fora da legenda analisados."))
              (prompt (strcat "\nLegenda/contexto: " (itoa mapped)
                              " | ambiguos: " (itoa ambiguous)
                              " | regras anteriores: " (itoa fallback)
                              " | sem mapeamento: " (itoa unmapped)))
              (prompt (strcat "\nDicionario da legenda: " dictionary-path))
              (prompt (strcat "\nItens semanticos: " map-path))
              (prompt (strcat "\nCatalogo semantico: " catalog-path))
              (prompt "\nCINZA PONTOS permaneceu intacto e o DWG nao foi alterado."))
            (prompt "\nNao foi possivel criar os arquivos v0.3.5.")))
        (prompt "\nOperacao cancelada. Nenhum arquivo foi criado.")))
    (prompt "\nNenhum objeto foi selecionado. O desenho nao foi modificado."))
  (setq *error* old-error)
  (princ)
)

(defun c:MASCARA_V034_ANALISAR ()
  (c:MASCARA_V035_ANALISAR)
)

;;; --------------------------------------------------------------------------
;;; Versao 0.4.2 - pre-visualizacao das acoes da mascara.
;;; Nenhum destes comandos cria layer, move, apaga ou explode entidades.
;;; Um ponto completo inclui o bloco principal e os componentes graficos ou
;;; textos associados espacialmente com confianca ALTA ou MEDIA.
;;; --------------------------------------------------------------------------

(setq *mcad4:legend-handles* nil)
(setq *mcad4:legend-selection* nil)
(setq *mcad4:legend-map* nil)
(setq *mcad4:items* nil)
(setq *mcad4:components* nil)

(defun mcad4:prepared-p ()
  (and *mcad4:legend-selection* *mcad4:items*)
)

;;; Item operacional: (ENTITY HANDLE INFO)
;;; Seed operacional: (ENTITY HANDLE NOME_BLOCO ITEM_ID BOX DISCIPLINA)
(defun mcad4:point-seeds
  (/ seeds item entity handle info data block-name box)
  (setq seeds nil)
  (foreach item *mcad4:items*
    (setq entity (nth 0 item))
    (setq handle (nth 1 item))
    (setq info (nth 2 item))
    (if (= (nth 2 info) "PONTO_PRINCIPAL")
      (progn
        (setq data (entget entity))
        (setq block-name (mcad33:block-name entity data))
        (setq box (mcad32:box entity data))
        (setq seeds
          (cons
            (list entity handle block-name handle box (nth 1 info))
            seeds)))))
  seeds
)

(defun mcad4:accepted-component-p (category confidence)
  (and
    (or (= category "REF_ELE_COMPONENTE")
        (= category "REF_ELE_ANOTACAO")
        (= category "REF_HID_COMPONENTE"))
    (or (= confidence "ALTA") (= confidence "MEDIA")))
)

;;; Componente operacional:
;;; (ENTITY HANDLE CATEGORIA DISCIPLINA SEED_HANDLE DISTANCIA CONFIANCA)
(defun mcad4:build-components
  (legend-handles
    / source seeds electric-seeds hydraulic-seeds index entity data handle
      classification category discipline nearest box seed-match distance
      annotation-p confidence count-electric-graphics count-electric-texts
      count-hydraulic-graphics count-rejected)

  (setq *mcad4:components* nil)
  (setq count-electric-graphics 0 count-electric-texts 0
        count-hydraulic-graphics 0 count-rejected 0)
  (setq seeds (mcad4:point-seeds))
  (setq electric-seeds
    (vl-remove-if-not
      '(lambda (seed) (= (nth 5 seed) "ELETRICO"))
      seeds))
  (setq hydraulic-seeds
    (vl-remove-if-not
      '(lambda (seed) (= (nth 5 seed) "HIDRAULICO"))
      seeds))
  (setq source (ssget "_X" '((410 . "Model"))))

  (if source
    (progn
      (setq index 0)
      (while (< index (sslength source))
        (setq entity (ssname source index))
        (setq data (entget entity))
        (setq handle (cdr (assoc 5 data)))
        (if (null handle) (setq handle ""))
        (if (not (member handle legend-handles))
          (progn
            (setq classification (mcad32:classification data))
            (setq category (nth 0 classification))
            (setq nearest nil discipline "")
            (cond
              ((or (= category "REF_ELE_COMPONENTE")
                   (= category "REF_ELE_ANOTACAO"))
                (setq discipline "ELETRICO")
                (setq box (mcad32:box entity data))
                (setq nearest (mcad32:nearest-seed box electric-seeds)))
              ((= category "REF_HID_COMPONENTE")
                (setq discipline "HIDRAULICO")
                (setq box (mcad32:box entity data))
                (setq nearest (mcad32:nearest-seed box hydraulic-seeds))))

            (if nearest
              (progn
                (setq seed-match (nth 0 nearest))
                (setq distance (nth 1 nearest))
                (setq annotation-p (= category "REF_ELE_ANOTACAO"))
                (setq confidence
                  (mcad32:association-confidence distance annotation-p))
                (if (mcad4:accepted-component-p category confidence)
                  (progn
                    (setq *mcad4:components*
                      (cons
                        (list entity handle category discipline
                              (nth 1 seed-match) distance confidence)
                        *mcad4:components*))
                    (cond
                      ((= category "REF_ELE_COMPONENTE")
                        (setq count-electric-graphics
                          (1+ count-electric-graphics)))
                      ((= category "REF_ELE_ANOTACAO")
                        (setq count-electric-texts
                          (1+ count-electric-texts)))
                      ((= category "REF_HID_COMPONENTE")
                        (setq count-hydraulic-graphics
                          (1+ count-hydraulic-graphics)))))
                  (setq count-rejected (1+ count-rejected)))))))
        ;; O indice deve avancar para todos os objetos, inclusive os que
        ;; nao pertencem a legenda. Mantê-lo fora do IF evita laco infinito.
        (setq index (1+ index)))))

  (list count-electric-graphics count-electric-texts
        count-hydraulic-graphics count-rejected)
)

(defun c:MASCARA_V04_PREPARAR
  (/ selection selection-index entity data entity-type handle layer-name
     block-name text-content box legend-handles texts blocks text-item
     block-item electric-title-x hydraulic-title-x match matched-text
     discipline legend-map source index map-entry info count-electric
     count-hydraulic count-base count-discard count-points component-counts)

  (setq *mcad4:legend-handles* nil)
  (setq *mcad4:legend-selection* nil)
  (setq *mcad4:legend-map* nil)
  (setq *mcad4:items* nil)
  (setq *mcad4:components* nil)
  (prompt
    "\nSelecione somente os dois quadros completos de simbologia.")
  (setq selection (aab:legend-selection))
  (if selection
    (progn
      (setq legend-handles nil texts nil blocks nil)
      (setq electric-title-x nil hydraulic-title-x nil)
      (setq selection-index 0)
      (while (< selection-index (sslength selection))
        (setq entity (ssname selection selection-index))
        (setq data (entget entity))
        (setq entity-type (cdr (assoc 0 data)))
        (setq handle (cdr (assoc 5 data)))
        (setq layer-name (cdr (assoc 8 data)))
        (if (null entity-type) (setq entity-type ""))
        (if (null handle) (setq handle ""))
        (if (null layer-name) (setq layer-name ""))
        (setq block-name (mcad33:block-name entity data))
        (setq text-content
          (if (or (= entity-type "TEXT")
                  (= entity-type "MTEXT")
                  (= entity-type "ATTRIB")
                  (= entity-type "ATTDEF"))
            (mcad32:text-content data) ""))
        (setq box (mcad32:box entity data))
        (setq legend-handles (cons handle legend-handles))
        (if (/= text-content "")
          (progn
            (setq text-item (list handle text-content box))
            (setq texts (cons text-item texts))
            (if (and (mcad3:contains text-content "SIMBOLOGIA")
                     (mcad3:contains text-content "HIDR"))
              (setq hydraulic-title-x (nth 6 box)))
            (if (and (mcad3:contains text-content "SIMBOLOGIA")
                     (not (mcad3:contains text-content "HIDR")))
              (setq electric-title-x (nth 6 box)))))
        (if (= entity-type "INSERT")
          (setq blocks
            (cons (list handle block-name box layer-name) blocks)))
        (setq selection-index (1+ selection-index)))

      (setq legend-map nil)
      (foreach block-item blocks
        (setq match (mcad33:find-row-text block-item texts))
        (if match
          (progn
            (setq matched-text (nth 0 match))
            (setq discipline
              (mcad34:legend-discipline
                block-item electric-title-x hydraulic-title-x))
            (setq legend-map
              (mcad34:add-map legend-map discipline (nth 1 block-item)
                (nth 1 matched-text))))))

      (setq *mcad4:legend-handles* legend-handles)
      (setq *mcad4:legend-selection* selection)
      (setq *mcad4:legend-map* legend-map)
      (setq *mcad4:items* nil)
      (setq count-electric 0 count-hydraulic 0 count-base 0
            count-discard 0 count-points 0)
      (setq source (ssget "_X" '((0 . "INSERT") (410 . "Model"))))
      (if source
        (progn
          (setq index 0)
          (while (< index (sslength source))
            (setq entity (ssname source index))
            (setq data (entget entity))
            (setq handle (cdr (assoc 5 data)))
            (if (null handle) (setq handle ""))
            (if (not (member handle legend-handles))
              (progn
                (setq block-name (mcad33:block-name entity data))
                (setq discipline
                  (mcad34:discipline-from-data data block-name))
                (setq map-entry
                  (mcad34:map-find legend-map discipline block-name))
                (if map-entry
                  (setq info (mcad34:info-from-map map-entry))
                  (setq info (mcad33:block-info data block-name)))
                (setq info
                  (mcad35:resolve-context entity data block-name info))
                (setq *mcad4:items*
                  (cons (list entity handle info) *mcad4:items*))
                (cond
                  ((= (nth 2 info) "PONTO_PRINCIPAL")
                    (setq count-points (1+ count-points))
                    (if (= (nth 1 info) "ELETRICO")
                      (setq count-electric (1+ count-electric)))
                    (if (= (nth 1 info) "HIDRAULICO")
                      (setq count-hydraulic (1+ count-hydraulic))))
                  ((= (nth 2 info) "DESCARTAVEL")
                    (setq count-discard (1+ count-discard)))
                  ((= (nth 1 info) "ARQUITETURA")
                    (setq count-base (1+ count-base))))))
            (setq index (1+ index)))))

      (setq component-counts (mcad4:build-components legend-handles))
      (sssetfirst nil nil)
      (prompt "\nPre-visualizacao v0.4.2 preparada.")
      (prompt (strcat "\nPontos finais: " (itoa count-points)
                      " | ELE=" (itoa count-electric)
                      " | HID=" (itoa count-hydraulic)))
      (prompt
        (strcat
          "\nAssociados seguros: ELE_GRAF="
          (itoa (nth 0 component-counts))
          " | ELE_TEXTO=" (itoa (nth 1 component-counts))
          " | HID_GRAF=" (itoa (nth 2 component-counts))
          " | baixa confianca excluidos="
          (itoa (nth 3 component-counts))))
      (prompt (strcat "\nBase/contexto: " (itoa count-base)
                      " | detalhes fora da legenda: "
                      (itoa count-discard)))
      (prompt (strcat "\nObjetos selecionados na legenda: "
                      (itoa (sslength selection))))
      (prompt
        "\nUse MASCARA_V04_ELETRICO, HIDRAULICO, BASE ou DESCARTAVEIS.")
      (prompt "\nO desenho nao foi modificado."))
    (prompt "\nNenhum objeto foi selecionado. O desenho nao foi modificado."))
  (princ)
)

(defun mcad4:preview
  (wanted include-components
    / result item entity handle info index selected-seeds component
      component-count point-count)
  (if (not (mcad4:prepared-p))
    (prompt "\nExecute primeiro MASCARA_V04_PREPARAR.")
    (progn
      (setq result (ssadd))
      (setq selected-seeds nil component-count 0 point-count 0)
      (if (= wanted "DESCARTAVEIS")
        (progn
          (setq index 0)
          (while (< index (sslength *mcad4:legend-selection*))
            (ssadd (ssname *mcad4:legend-selection* index) result)
            (setq index (1+ index)))))
      (foreach item *mcad4:items*
        (setq entity (nth 0 item))
        (setq handle (nth 1 item))
        (setq info (nth 2 item))
        (cond
          ((and (= wanted "ELETRICO")
                (= (nth 1 info) "ELETRICO")
                (= (nth 2 info) "PONTO_PRINCIPAL"))
            (ssadd entity result)
            (setq selected-seeds (cons handle selected-seeds))
            (setq point-count (1+ point-count)))
          ((and (= wanted "HIDRAULICO")
                (= (nth 1 info) "HIDRAULICO")
                (= (nth 2 info) "PONTO_PRINCIPAL"))
            (ssadd entity result)
            (setq selected-seeds (cons handle selected-seeds))
            (setq point-count (1+ point-count)))
          ((and (= wanted "BASE")
                (= (nth 1 info) "ARQUITETURA"))
            (ssadd entity result))
          ((and (= wanted "DESCARTAVEIS")
                (= (nth 2 info) "DESCARTAVEL"))
            (ssadd entity result))
          ((and (= wanted "PONTOS")
                (= (nth 2 info) "PONTO_PRINCIPAL"))
            (ssadd entity result)
            (setq selected-seeds (cons handle selected-seeds))
            (setq point-count (1+ point-count)))))

      (if include-components
        (foreach component *mcad4:components*
          (if (member (nth 4 component) selected-seeds)
            (progn
              (ssadd (nth 0 component) result)
              (setq component-count (1+ component-count))))))

      (sssetfirst nil nil)
      (if (> (sslength result) 0) (sssetfirst nil result))
      (prompt (strcat "\n" wanted ": " (itoa (sslength result))
                      " objetos selecionados temporariamente."))
      (if include-components
        (prompt
          (strcat "\nPontos principais: " (itoa point-count)
                  " | componentes/textos associados: "
                  (itoa component-count))))
      (prompt "\nNenhuma entidade foi alterada. Pressione ESC para limpar.")))
  (princ)
)

(defun c:MASCARA_V04_ELETRICO ()
  (mcad4:preview "ELETRICO" T)
)

(defun c:MASCARA_V04_HIDRAULICO ()
  (mcad4:preview "HIDRAULICO" T)
)

(defun c:MASCARA_V04_ELETRICO_BLOCOS ()
  (mcad4:preview "ELETRICO" nil)
)

(defun c:MASCARA_V04_HIDRAULICO_BLOCOS ()
  (mcad4:preview "HIDRAULICO" nil)
)

(defun c:MASCARA_V04_BASE ()
  (mcad4:preview "BASE" nil)
)

(defun c:MASCARA_V04_DESCARTAVEIS ()
  (mcad4:preview "DESCARTAVEIS" nil)
)

(defun c:MASCARA_V04_PONTOS ()
  (mcad4:preview "PONTOS" T)
)

(defun c:MASCARA_V04_TIPO
  (/ wanted result item entity handle info selected-seeds component
     component-count point-count)
  (if (not (mcad4:prepared-p))
    (prompt "\nExecute primeiro MASCARA_V04_PREPARAR.")
    (progn
      (setq wanted
        (strcase
          (getstring T
            "\nDigite o CODIGO_SEMANTICO, por exemplo ELE_TOMADA_BAIXA: ")))
      (setq result (ssadd))
      (setq selected-seeds nil component-count 0 point-count 0)
      (foreach item *mcad4:items*
        (setq entity (nth 0 item))
        (setq handle (nth 1 item))
        (setq info (nth 2 item))
        (if (= wanted (strcase (nth 3 info)))
          (progn
            (ssadd entity result)
            (setq selected-seeds (cons handle selected-seeds))
            (setq point-count (1+ point-count)))))
      (foreach component *mcad4:components*
        (if (member (nth 4 component) selected-seeds)
          (progn
            (ssadd (nth 0 component) result)
            (setq component-count (1+ component-count)))))
      (sssetfirst nil nil)
      (if (> (sslength result) 0) (sssetfirst nil result))
      (prompt (strcat "\n" wanted ": " (itoa (sslength result))
                      " objetos selecionados temporariamente."))
      (prompt
        (strcat "\nPontos principais: " (itoa point-count)
                " | componentes/textos associados: "
                (itoa component-count)))
      (prompt "\nNenhuma entidade foi alterada.")))
  (princ)
)

(defun c:MASCARA_V04_LIMPAR ()
  (c:MASCARA_V03_LIMPAR)
)

(prompt
  "\nRotina v0.4.2 carregada. Digite MASCARA_V04_PREPARAR para iniciar."
)
(princ)
