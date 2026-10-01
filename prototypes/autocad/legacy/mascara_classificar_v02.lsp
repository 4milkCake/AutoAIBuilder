;;; mascara_classificar_v02.lsp
;;; Versao 0.2 - Diagnostico e classificacao preliminar para mascaras CAD.
;;; Compativel com o fluxo observado no AutoCAD 2025.
;;; Nao apaga, move, explode, cria ou altera objetos do desenho.
;;;
;;; Comando disponibilizado:
;;;   MASCARA_CLASSIFICAR

(defun mcad2:csv-field (value / text result index char)
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

(defun mcad2:add-count (key counts / found)
  (if (setq found (assoc key counts))
    (subst (cons key (1+ (cdr found))) found counts)
    (cons (cons key 1) counts)
  )
)

(defun mcad2:base-name (file-name / length-name extension)
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

(defun mcad2:layer-status (layer-name / record flags color-number status)
  (setq record (tblsearch "LAYER" layer-name))
  (if record
    (progn
      (setq flags (cdr (assoc 70 record)))
      (setq color-number (cdr (assoc 62 record)))
      (if (null flags) (setq flags 0))
      (if (null color-number) (setq color-number 7))
      (setq status (if (< color-number 0) "DESLIGADO" "LIGADO"))
      (if (/= 0 (logand flags 1))
        (setq status (strcat status "|CONGELADO"))
      )
      (if (/= 0 (logand flags 4))
        (setq status (strcat status "|BLOQUEADO"))
      )
      status
    )
    "NAO_LOCALIZADO"
  )
)

(defun mcad2:contains (text fragment)
  (and text fragment
       (wcmatch (strcase text) (strcat "*" (strcase fragment) "*")))
)

(defun mcad2:block-record (block-name)
  (if (and block-name (/= block-name ""))
    (tblsearch "BLOCK" block-name)
    nil
  )
)

(defun mcad2:xref-status (block-name / record flags)
  (setq record (mcad2:block-record block-name))
  (if record
    (progn
      (setq flags (cdr (assoc 70 record)))
      (if (null flags) (setq flags 0))
      (if (/= 0 (logand flags 12)) "SIM" "NAO")
    )
    "NAO"
  )
)

(defun mcad2:xref-path (block-name / record path)
  (setq record (mcad2:block-record block-name))
  (if record
    (progn
      (setq path (cdr (assoc 1 record)))
      (if path path "")
    )
    ""
  )
)

(defun mcad2:block-size
  (block-name visited / upper-name block-record entity entity-data
              entity-type child-name total xref-state)

  (setq upper-name (strcase block-name))
  (setq xref-state (mcad2:xref-status block-name))

  (cond
    ((= xref-state "SIM") -1)
    ((member upper-name visited) 0)
    ((null (setq block-record (tblobjname "BLOCK" block-name))) 0)
    (T
      (setq total 0)
      (setq entity (entnext block-record))
      (while entity
        (setq entity-data (entget entity))
        (setq entity-type (cdr (assoc 0 entity-data)))
        (if (= entity-type "ENDBLK")
          (setq entity nil)
          (progn
            (setq total (1+ total))
            (if (= entity-type "INSERT")
              (progn
                (setq child-name (cdr (assoc 2 entity-data)))
                (if child-name
                  (setq total
                    (+ total
                       (max 0
                         (mcad2:block-size
                           child-name
                           (cons upper-name visited)
                         )
                       )
                    )
                  )
                )
              )
            )
            (setq entity (entnext entity))
          )
        )
      )
      total
    )
  )
)

(defun mcad2:classification
  (layer-name entity-type block-name xref-state / layer-upper block-upper)

  (setq layer-upper (strcase layer-name))
  (setq block-upper (strcase block-name))

  (cond
    ((= xref-state "SIM")
      (list "REVISAR" "Bloco identificado como Xref; confirmar tratamento antes de explodir"))

    ((or (= entity-type "DIMENSION")
         (= entity-type "LEADER")
         (= layer-upper "CHAMADA - COTAS"))
      (list "REMOVER" "Cotas e chamadas foram removidas na mascara manual"))

    ((= layer-upper "DEFPOINTS")
      (list "REMOVER" "Layer auxiliar de definicao de cotas"))

    ((= layer-upper "TABELA")
      (list "REMOVER" "Geometria de tabela ausente na mascara manual"))

    ((or (= block-upper "LOGO ICON")
         (= block-upper "NOME DO_DESENHO")
         (= block-upper "CINZA PONTOS"))
      (list "REMOVER" "Bloco auxiliar ausente na mascara manual"))

    ((or (mcad2:contains layer-upper "SIMBOLOGIA PONTOS")
         (mcad2:contains layer-upper "HID -"))
      (list "MANTER" "Todos os pontos existentes devem ser preservados"))

    ((or (mcad2:contains layer-upper "MOBILI")
         (mcad2:contains layer-upper "PROJE")
         (mcad2:contains layer-upper "HACH")
         (mcad2:contains layer-upper "TEXT")
         (mcad2:contains layer-upper "CHAMADA - T")
         (= layer-upper "COR 10")
         (= layer-upper "TX-3"))
      (list "MANTER" "Arquitetura, mobiliario, projecoes e textos uteis foram preservados"))

    ((= block-upper "BASE 1")
      (list "MANTER" "Bloco presente no original e na mascara manual"))

    ((or (= entity-type "TEXT")
         (= entity-type "MTEXT")
         (= entity-type "HATCH"))
      (list "MANTER" "Tipo de objeto predominantemente preservado na mascara manual"))

    (T
      (list "REVISAR" "Regra ainda nao confirmada para este layer, tipo ou bloco"))
  )
)

(defun c:MASCARA_CLASSIFICAR
  (/ *error* old-error output-file file-handle drawing-path drawing-name
     base-name default-output selection index entity entity-data layer-name
     entity-type block-name key counts item quantity total status xref-state
     xref-path block-size nested-total nested-text classification class-name
     reason keep-total remove-total review-total)

  (setq old-error *error*)

  (defun *error* (message)
    (if file-handle
      (progn
        (close file-handle)
        (setq file-handle nil)
      )
    )
    (setq *error* old-error)
    (if (and message
             (/= message "Function cancelled")
             (/= message "quit / exit abort"))
      (prompt (strcat "\nErro em MASCARA_CLASSIFICAR: " message))
    )
    (princ)
  )

  (setq drawing-path (getvar "DWGPREFIX"))
  (setq drawing-name (getvar "DWGNAME"))

  (if (= drawing-path "")
    (prompt "\nSalve o desenho DWG antes de executar MASCARA_CLASSIFICAR.")
    (progn
      (setq base-name (mcad2:base-name drawing-name))
      (setq default-output
        (strcat drawing-path base-name "_classificacao_mascara_v02.csv")
      )

      (setq output-file
        (getfiled
          "Salvar classificacao da mascara"
          default-output
          "csv"
          1
        )
      )

      (if output-file
        (progn
          (prompt "\nClassificando objetos do Model Space. O desenho nao sera alterado...")
          (setq selection (ssget "_X" '((410 . "Model"))))
          (setq counts nil total 0)
          (setq keep-total 0 remove-total 0 review-total 0)

          (if selection
            (progn
              (setq index 0)
              (while (< index (sslength selection))
                (setq entity (ssname selection index))
                (setq entity-data (entget entity))
                (setq layer-name (cdr (assoc 8 entity-data)))
                (setq entity-type (cdr (assoc 0 entity-data)))
                (setq block-name "")

                (if (= entity-type "INSERT")
                  (progn
                    (setq block-name (cdr (assoc 2 entity-data)))
                    (if (null block-name) (setq block-name ""))
                  )
                )

                (if (null layer-name) (setq layer-name "SEM_LAYER"))
                (if (null entity-type) (setq entity-type "DESCONHECIDO"))

                (setq key (list layer-name entity-type block-name))
                (setq counts (mcad2:add-count key counts))
                (setq total (1+ total))
                (setq index (1+ index))
              )
            )
          )

          (setq file-handle (open output-file "w"))

          (if file-handle
            (progn
              (write-line
                "\"ARQUIVO\";\"LAYER\";\"TIPO_OBJETO\";\"NOME_BLOCO\";\"QUANTIDADE\";\"ESTADO_LAYER\";\"CLASSIFICACAO\";\"MOTIVO\";\"EH_XREF\";\"CAMINHO_XREF\";\"OBJETOS_INTERNOS_POR_BLOCO\";\"OBJETOS_INTERNOS_TOTAL_ESTIMADO\""
                file-handle
              )

              (foreach item counts
                (setq key (car item))
                (setq quantity (cdr item))
                (setq layer-name (nth 0 key))
                (setq entity-type (nth 1 key))
                (setq block-name (nth 2 key))
                (setq status (mcad2:layer-status layer-name))
                (setq xref-state (mcad2:xref-status block-name))
                (setq xref-path (mcad2:xref-path block-name))
                (setq block-size 0)

                (if (= entity-type "INSERT")
                  (setq block-size (mcad2:block-size block-name nil))
                )

                (if (< block-size 0)
                  (progn
                    (setq nested-text "XREF")
                    (setq nested-total "XREF")
                  )
                  (progn
                    (setq nested-text (itoa block-size))
                    (setq nested-total (itoa (* block-size quantity)))
                  )
                )

                (setq classification
                  (mcad2:classification
                    layer-name entity-type block-name xref-state
                  )
                )
                (setq class-name (nth 0 classification))
                (setq reason (nth 1 classification))

                (cond
                  ((= class-name "MANTER")
                    (setq keep-total (+ keep-total quantity)))
                  ((= class-name "REMOVER")
                    (setq remove-total (+ remove-total quantity)))
                  (T
                    (setq review-total (+ review-total quantity)))
                )

                (write-line
                  (strcat
                    (mcad2:csv-field drawing-name) ";"
                    (mcad2:csv-field layer-name) ";"
                    (mcad2:csv-field entity-type) ";"
                    (mcad2:csv-field block-name) ";"
                    (mcad2:csv-field (itoa quantity)) ";"
                    (mcad2:csv-field status) ";"
                    (mcad2:csv-field class-name) ";"
                    (mcad2:csv-field reason) ";"
                    (mcad2:csv-field xref-state) ";"
                    (mcad2:csv-field xref-path) ";"
                    (mcad2:csv-field nested-text) ";"
                    (mcad2:csv-field nested-total)
                  )
                  file-handle
                )
              )

              (close file-handle)
              (setq file-handle nil)

              (prompt (strcat "\nClassificacao concluida. " (itoa total) " objetos analisados."))
              (prompt (strcat "\nMANTER: " (itoa keep-total)))
              (prompt (strcat " | REMOVER: " (itoa remove-total)))
              (prompt (strcat " | REVISAR: " (itoa review-total)))
              (prompt (strcat "\nRelatorio salvo em: " output-file))
              (prompt "\nO desenho nao foi modificado.")
            )
            (prompt "\nNao foi possivel criar o arquivo CSV selecionado.")
          )
        )
        (prompt "\nOperacao cancelada. Nenhum arquivo foi criado.")
      )
    )
  )

  (setq *error* old-error)
  (princ)
)

(prompt
  "\nRotina MASCARA_CLASSIFICAR carregada. Digite MASCARA_CLASSIFICAR para iniciar."
)
(princ)
