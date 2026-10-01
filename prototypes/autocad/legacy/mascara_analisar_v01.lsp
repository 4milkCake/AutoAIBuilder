;;; mascara_analisar_v01.lsp
;;; Versao 0.1 - Inventario somente leitura para preparacao de mascaras CAD.
;;; Nao apaga, move, cria ou altera objetos do desenho.
;;;
;;; Comando disponibilizado:
;;;   MASCARA_ANALISAR

(defun mcad:csv-field (value / text result index char)
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

(defun mcad:add-count (key counts / found)
  (if (setq found (assoc key counts))
    (subst (cons key (1+ (cdr found))) found counts)
    (cons (cons key 1) counts)
  )
)

(defun mcad:base-name (file-name / length-name extension)
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

(defun mcad:layer-status (layer-name / record flags color-number status)
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

(defun c:MASCARA_ANALISAR
  (/ *error* old-error output-file file-handle drawing-path drawing-name
     base-name default-output selection index entity entity-data layer-name
     entity-type block-name key counts item quantity total status)

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
      (prompt (strcat "\nErro em MASCARA_ANALISAR: " message))
    )
    (princ)
  )

  (setq drawing-path (getvar "DWGPREFIX"))
  (setq drawing-name (getvar "DWGNAME"))

  (if (= drawing-path "")
    (prompt "\nSalve o desenho DWG antes de executar MASCARA_ANALISAR.")
    (progn
      (setq base-name (mcad:base-name drawing-name))
      (setq default-output
        (strcat drawing-path base-name "_analise_mascara.csv")
      )

      (setq output-file
        (getfiled
          "Salvar inventario da mascara"
          default-output
          "csv"
          1
        )
      )

      (if output-file
        (progn
          (prompt "\nLendo objetos do Model Space. O desenho nao sera alterado...")
          (setq selection (ssget "_X" '((410 . "Model"))))
          (setq counts nil total 0)

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
                (setq counts (mcad:add-count key counts))
                (setq total (1+ total))
                (setq index (1+ index))
              )
            )
          )

          (setq file-handle (open output-file "w"))

          (if file-handle
            (progn
              (write-line
                "\"ARQUIVO\";\"ESPACO\";\"LAYER\";\"TIPO_OBJETO\";\"NOME_BLOCO\";\"QUANTIDADE\";\"ESTADO_LAYER\""
                file-handle
              )

              (foreach item counts
                (setq key (car item))
                (setq quantity (cdr item))
                (setq layer-name (nth 0 key))
                (setq entity-type (nth 1 key))
                (setq block-name (nth 2 key))
                (setq status (mcad:layer-status layer-name))

                (write-line
                  (strcat
                    (mcad:csv-field drawing-name) ";"
                    (mcad:csv-field "Model") ";"
                    (mcad:csv-field layer-name) ";"
                    (mcad:csv-field entity-type) ";"
                    (mcad:csv-field block-name) ";"
                    (mcad:csv-field (itoa quantity)) ";"
                    (mcad:csv-field status)
                  )
                  file-handle
                )
              )

              (close file-handle)
              (setq file-handle nil)

              (prompt
                (strcat
                  "\nAnalise concluida. "
                  (itoa total)
                  " objetos contabilizados."
                )
              )
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
  "\nRotina MASCARA_ANALISAR carregada. Digite MASCARA_ANALISAR para iniciar."
)
(princ)
