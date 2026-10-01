;;; mascara_builder_piloto_v08.lsp
;;; Versao 0.8.0 - diagnostico de direcao para o piloto no AltoQi Builder.
;;;
;;; OBJETIVO:
;;; - Ler apenas os blocos da layer PONTOS_ELE_TOMADA_MEDIA.
;;; - Registrar as propriedades dinamicas que controlam a direcao visual.
;;; - Calibrar dois pontos ja validados: um voltado para BAIXO e outro
;;;   voltado para ESQUERDA.
;;; - Exportar um CSV de diagnostico para montar o primeiro lote no Builder.
;;;
;;; SEGURANCA:
;;; - Nao apaga, move, explode, cria layer ou altera entidades.
;;; - Nao le o interior de CINZA PONTOS.
;;; - Atua somente em copia cujo nome contenha MASCARA ou COPIA.
;;; - A pre-visualizacao altera apenas a selecao temporaria.
;;;
;;; COMANDOS:
;;;   MASCARA_V08_STATUS
;;;   MASCARA_V08_PREVISUALIZAR
;;;   MASCARA_V08_CALIBRAR
;;;   MASCARA_V08_INSPECIONAR
;;;   MASCARA_V08_EXPORTAR

(vl-load-com)

(setq *mcad8:version* "0.8.0")
(setq *mcad8:layer* "PONTOS_ELE_TOMADA_MEDIA")
(setq *mcad8:calibration* nil)
(setq *mcad8:last-file* nil)

(defun mcad8:replace-all (text old new / position)
  (setq text (if text text ""))
  (if (and old (/= old ""))
    (while (setq position (vl-string-search old text))
      (setq text
        (strcat
          (substr text 1 position)
          new
          (substr text (+ position (strlen old) 1))))))
  text
)

(defun mcad8:csv-field (value / text)
  (setq text
    (cond
      ((null value) "")
      ((= (type value) 'STR) value)
      ((= (type value) 'INT) (itoa value))
      ((= (type value) 'REAL) (rtos value 2 8))
      (T (vl-princ-to-string value))))
  (setq text (mcad8:replace-all text "\"" "\"\""))
  (strcat "\"" text "\"")
)

(defun mcad8:number-text (value)
  (if (numberp value)
    (rtos value 2 8)
    "")
)

(defun mcad8:join (values separator / result value)
  (setq result "")
  (foreach value values
    (setq result
      (strcat result (if (= result "") "" separator) value)))
  result
)

(defun mcad8:safe-copy-p (/ name upper)
  (setq name (vl-filename-base (getvar "DWGNAME")))
  (setq upper (strcase name))
  (or
    (not (null (vl-string-search "MASCARA" upper)))
    (not (null (vl-string-search "COPIA" upper))))
)

(defun mcad8:entity-layer (entity / data)
  (setq data (entget entity))
  (if data (cdr (assoc 8 data)) "")
)

(defun mcad8:eligible-p (entity / data)
  (setq data (entget entity))
  (and
    data
    (= (cdr (assoc 0 data)) "INSERT")
    (= (strcase (cdr (assoc 8 data))) (strcase *mcad8:layer*)))
)

(defun mcad8:medium-selection ()
  (ssget "_X"
    (list
      '(0 . "INSERT")
      (cons 8 *mcad8:layer*)
      (cons 410 (getvar "CTAB"))))
)

(defun mcad8:point-value (point index)
  (if (and point (> (length point) index))
    (nth index point)
    0.0)
)

(defun mcad8:insertion-point (data / point)
  (setq point (cdr (assoc 10 data)))
  (if point
    (list
      (mcad8:point-value point 0)
      (mcad8:point-value point 1)
      (mcad8:point-value point 2))
    '(0.0 0.0 0.0))
)

(defun mcad8:center-point (entity / object minimum maximum result)
  (setq object (vlax-ename->vla-object entity))
  (setq result
    (vl-catch-all-apply
      'vla-GetBoundingBox
      (list object 'minimum 'maximum)))
  (if (vl-catch-all-error-p result)
    (mcad8:insertion-point (entget entity))
    (progn
      (setq minimum (vlax-safearray->list minimum))
      (setq maximum (vlax-safearray->list maximum))
      (list
        (/ (+ (nth 0 minimum) (nth 0 maximum)) 2.0)
        (/ (+ (nth 1 minimum) (nth 1 maximum)) 2.0)
        (/ (+ (nth 2 minimum) (nth 2 maximum)) 2.0))))
)

(defun mcad8:effective-name (object / result)
  (setq result
    (vl-catch-all-apply 'vla-get-EffectiveName (list object)))
  (if (vl-catch-all-error-p result)
    (vla-get-Name object)
    result)
)

(defun mcad8:unwrap-variant (value / result)
  (setq result value)
  (while (= (type result) 'VARIANT)
    (setq result (vlax-variant-value result)))
  result
)

(defun mcad8:any-value-text (value / raw value-type)
  (setq raw (mcad8:unwrap-variant value))
  (setq value-type (type raw))
  (cond
    ((null raw) "")
    ((= value-type 'STR) raw)
    ((= value-type 'INT) (itoa raw))
    ((= value-type 'REAL) (rtos raw 2 8))
    ((= value-type 'SYM) (vl-symbol-name raw))
    ((= value-type 'LIST)
      (mcad8:join (mapcar 'mcad8:any-value-text raw) ","))
    (T (vl-princ-to-string raw)))
)

(defun mcad8:dynamic-properties
  (entity / object is-dynamic result properties output property
    property-name property-value readonly)
  (setq output nil)
  (setq object (vlax-ename->vla-object entity))
  (setq is-dynamic
    (vl-catch-all-apply 'vla-get-IsDynamicBlock (list object)))
  (if (and
        (not (vl-catch-all-error-p is-dynamic))
        (= is-dynamic :vlax-true))
    (progn
      (setq result
        (vl-catch-all-apply
          'vlax-invoke
          (list object 'GetDynamicBlockProperties)))
      (if (not (vl-catch-all-error-p result))
        (progn
          (setq properties result)
          (foreach property properties
            (setq property-name
              (vl-catch-all-apply
                'vlax-get-property
                (list property 'PropertyName)))
            (setq property-value
              (vl-catch-all-apply
                'vlax-get-property
                (list property 'Value)))
            (setq readonly
              (vl-catch-all-apply
                'vlax-get-property
                (list property 'ReadOnly)))
            (if (vl-catch-all-error-p property-name)
              (setq property-name "PROPRIEDADE_SEM_NOME"))
            (if (vl-catch-all-error-p property-value)
              (setq property-value "ERRO_DE_LEITURA"))
            (if (vl-catch-all-error-p readonly)
              (setq readonly :vlax-false))
            (setq output
              (cons
                (list
                  (mcad8:any-value-text property-name)
                  (mcad8:any-value-text property-value)
                  (if (= readonly :vlax-true) "SIM" "NAO"))
                output)))))))
  (reverse output)
)

(defun mcad8:property-list-text (properties / output item)
  (setq output nil)
  (foreach item properties
    (setq output
      (cons
        (strcat
          (nth 0 item) "="
          (nth 1 item)
          " [SOMENTE_LEITURA=" (nth 2 item) "]")
        output)))
  (mcad8:join (reverse output) " | ")
)

(defun mcad8:entity-record
  (entity direction
    / data object handle layer name effective insertion center rotation
      scale-x scale-y scale-z properties)
  (setq data (entget entity))
  (setq object (vlax-ename->vla-object entity))
  (setq handle (vla-get-Handle object))
  (setq layer (vla-get-Layer object))
  (setq name (vla-get-Name object))
  (setq effective (mcad8:effective-name object))
  (setq insertion (mcad8:insertion-point data))
  (setq center (mcad8:center-point entity))
  (setq rotation
    (* (if (assoc 50 data) (cdr (assoc 50 data)) 0.0)
       (/ 180.0 pi)))
  (setq scale-x (if (assoc 41 data) (cdr (assoc 41 data)) 1.0))
  (setq scale-y (if (assoc 42 data) (cdr (assoc 42 data)) 1.0))
  (setq scale-z (if (assoc 43 data) (cdr (assoc 43 data)) 1.0))
  (setq properties (mcad8:dynamic-properties entity))
  (list
    entity handle direction layer name effective insertion center rotation
    scale-x scale-y scale-z properties)
)

(defun mcad8:calibration-direction (handle / pair)
  (if (setq pair (assoc handle *mcad8:calibration*))
    (cdr pair)
    "")
)

(defun mcad8:select-medium-point (message / selected entity)
  (setq entity nil)
  (while (null entity)
    (setq selected (entsel message))
    (cond
      ((null selected)
        (prompt "\nSelecao cancelada.")
        (setq entity 'CANCELADO))
      ((mcad8:eligible-p (car selected))
        (setq entity (car selected)))
      (T
        (prompt
          (strcat
            "\nSelecione um bloco da layer "
            *mcad8:layer* ".")))))
  (if (= entity 'CANCELADO) nil entity)
)

(defun mcad8:print-record (record / properties item)
  (if record
    (progn
      (prompt
        (strcat
          "\nHANDLE: " (nth 1 record)
          " | DIRECAO: "
          (if (= (nth 2 record) "") "NAO_CALIBRADA" (nth 2 record))))
      (prompt
        (strcat
          "\nBLOCO: " (nth 4 record)
          " | NOME EFETIVO: " (nth 5 record)
          " | LAYER: " (nth 3 record)))
      (prompt
        (strcat
          "\nROTACAO: " (mcad8:number-text (nth 8 record))
          " | ESCALAS: "
          (mcad8:number-text (nth 9 record)) ","
          (mcad8:number-text (nth 10 record)) ","
          (mcad8:number-text (nth 11 record))))
      (setq properties (nth 12 record))
      (prompt
        (strcat
          "\nPROPRIEDADES DINAMICAS: "
          (itoa (length properties))))
      (foreach item properties
        (prompt
          (strcat
            "\n  - " (nth 0 item)
            " = " (nth 1 item)
            " | somente leitura: " (nth 2 item))))))
  record
)

(defun mcad8:default-output-path ()
  (strcat
    (getvar "DWGPREFIX")
    (vl-filename-base (getvar "DWGNAME"))
    "_diagnostico_direcoes_v08.csv")
)

(defun mcad8:write-header (file)
  (write-line
    "\"VERSAO\";\"ARQUIVO_DWG\";\"CAMINHO_DWG\";\"HANDLE\";\"DIRECAO_CALIBRACAO\";\"LAYER\";\"NOME_BLOCO\";\"NOME_EFETIVO\";\"X_INSERCAO\";\"Y_INSERCAO\";\"Z_INSERCAO\";\"X_CENTRO\";\"Y_CENTRO\";\"Z_CENTRO\";\"ROTACAO_GRAUS\";\"ESCALA_X\";\"ESCALA_Y\";\"ESCALA_Z\";\"QTD_PROPRIEDADES_DINAMICAS\";\"PROPRIEDADES_DINAMICAS\""
    file)
)

(defun mcad8:write-record (file record / insertion center properties)
  (setq insertion (nth 6 record))
  (setq center (nth 7 record))
  (setq properties (nth 12 record))
  (write-line
    (strcat
      (mcad8:csv-field *mcad8:version*) ";"
      (mcad8:csv-field (getvar "DWGNAME")) ";"
      (mcad8:csv-field
        (strcat (getvar "DWGPREFIX") (getvar "DWGNAME"))) ";"
      (mcad8:csv-field (nth 1 record)) ";"
      (mcad8:csv-field (nth 2 record)) ";"
      (mcad8:csv-field (nth 3 record)) ";"
      (mcad8:csv-field (nth 4 record)) ";"
      (mcad8:csv-field (nth 5 record)) ";"
      (mcad8:csv-field
        (mcad8:number-text (mcad8:point-value insertion 0))) ";"
      (mcad8:csv-field
        (mcad8:number-text (mcad8:point-value insertion 1))) ";"
      (mcad8:csv-field
        (mcad8:number-text (mcad8:point-value insertion 2))) ";"
      (mcad8:csv-field
        (mcad8:number-text (mcad8:point-value center 0))) ";"
      (mcad8:csv-field
        (mcad8:number-text (mcad8:point-value center 1))) ";"
      (mcad8:csv-field
        (mcad8:number-text (mcad8:point-value center 2))) ";"
      (mcad8:csv-field (mcad8:number-text (nth 8 record))) ";"
      (mcad8:csv-field (mcad8:number-text (nth 9 record))) ";"
      (mcad8:csv-field (mcad8:number-text (nth 10 record))) ";"
      (mcad8:csv-field (mcad8:number-text (nth 11 record))) ";"
      (mcad8:csv-field (itoa (length properties))) ";"
      (mcad8:csv-field (mcad8:property-list-text properties)))
    file)
)

(defun c:MASCARA_V08_STATUS (/ selection count)
  (prompt "\nDiagnostico de direcao v0.8:")
  (if (not (mcad8:safe-copy-p))
    (prompt
      "\nBLOQUEIO: use uma copia cujo nome contenha MASCARA ou COPIA.")
    (progn
      (setq selection (mcad8:medium-selection))
      (setq count (if selection (sslength selection) 0))
      (prompt
        (strcat
          "\nLayer analisada: " *mcad8:layer*
          " | blocos encontrados: " (itoa count)))
      (prompt
        (strcat
          "\nReferencias calibradas nesta sessao: "
          (itoa (length *mcad8:calibration*)) " de 2."))
      (if (= (length *mcad8:calibration*) 2)
        (prompt "\nPronto para MASCARA_V08_EXPORTAR.")
        (prompt "\nExecute MASCARA_V08_CALIBRAR antes de exportar."))))
  (princ)
)

(defun c:MASCARA_V08_PREVISUALIZAR (/ selection)
  (if (not (mcad8:safe-copy-p))
    (prompt
      "\nBLOQUEIO: use uma copia cujo nome contenha MASCARA ou COPIA.")
    (progn
      (setq selection (mcad8:medium-selection))
      (if selection
        (progn
          (sssetfirst nil selection)
          (prompt
            (strcat
              "\nTomadas medias selecionadas temporariamente: "
              (itoa (sslength selection))
              ". Nenhuma entidade foi alterada.")))
        (prompt
          (strcat "\nNenhum bloco encontrado em " *mcad8:layer* ".")))))
  (princ)
)

(defun c:MASCARA_V08_CALIBRAR
  (/ down left down-handle left-handle)
  (setq *mcad8:calibration* nil)
  (if (not (mcad8:safe-copy-p))
    (prompt
      "\nBLOQUEIO: use uma copia cujo nome contenha MASCARA ou COPIA.")
    (progn
      (prompt
        "\nSelecione exatamente os dois pontos usados nos testes do Builder.")
      (setq down
        (mcad8:select-medium-point
          "\n1/2 - Selecione a tomada MEDIA voltada para BAIXO: "))
      (if down
        (progn
          (setq down-handle
            (vla-get-Handle (vlax-ename->vla-object down)))
          (setq left
            (mcad8:select-medium-point
              "\n2/2 - Selecione a tomada MEDIA voltada para ESQUERDA: "))
          (if left
            (progn
              (setq left-handle
                (vla-get-Handle (vlax-ename->vla-object left)))
              (if (= down-handle left-handle)
                (prompt
                  "\nCALIBRACAO CANCELADA: o mesmo ponto foi selecionado duas vezes.")
                (progn
                  (setq *mcad8:calibration*
                    (list
                      (cons down-handle "BAIXO")
                      (cons left-handle "ESQUERDA")))
                  (prompt "\nCalibracao registrada:")
                  (mcad8:print-record
                    (mcad8:entity-record down "BAIXO"))
                  (mcad8:print-record
                    (mcad8:entity-record left "ESQUERDA"))
                  (prompt
                    "\nAgora execute MASCARA_V08_EXPORTAR.")))))))))
  (princ)
)

(defun c:MASCARA_V08_INSPECIONAR (/ entity object handle direction)
  (if (not (mcad8:safe-copy-p))
    (prompt
      "\nBLOQUEIO: use uma copia cujo nome contenha MASCARA ou COPIA.")
    (progn
      (setq entity
        (mcad8:select-medium-point
          "\nSelecione uma tomada MEDIA para inspecionar: "))
      (if entity
        (progn
          (setq object (vlax-ename->vla-object entity))
          (setq handle (vla-get-Handle object))
          (setq direction (mcad8:calibration-direction handle))
          (mcad8:print-record
            (mcad8:entity-record entity direction))))))
  (princ)
)

(defun c:MASCARA_V08_EXPORTAR
  (/ selection count chosen file index entity object handle direction record
    exported)
  (cond
    ((not (mcad8:safe-copy-p))
      (prompt
        "\nBLOQUEIO: use uma copia cujo nome contenha MASCARA ou COPIA."))
    ((/= (length *mcad8:calibration*) 2)
      (prompt
        "\nBLOQUEIO: execute MASCARA_V08_CALIBRAR nesta sessao."))
    ((null (setq selection (mcad8:medium-selection)))
      (prompt
        (strcat "\nNenhum bloco encontrado em " *mcad8:layer* ".")))
    (T
      (setq count (sslength selection))
      (setq chosen
        (getfiled
          "Salvar diagnostico de direcoes v0.8"
          (mcad8:default-output-path)
          "csv"
          1))
      (if chosen
        (progn
          (if (findfile chosen)
            (vl-file-delete chosen))
          (setq file (open chosen "w"))
          (if file
            (progn
              (mcad8:write-header file)
              (setq index 0 exported 0)
              (while (< index count)
                (setq entity (ssname selection index))
                (setq object (vlax-ename->vla-object entity))
                (setq handle (vla-get-Handle object))
                (setq direction (mcad8:calibration-direction handle))
                (setq record
                  (mcad8:entity-record entity direction))
                (mcad8:write-record file record)
                (setq exported (1+ exported))
                (setq index (1+ index)))
              (close file)
              (setq file nil)
              (setq *mcad8:last-file* chosen)
              (prompt
                (strcat
                  "\nDiagnostico v0.8 concluido: "
                  (itoa exported) " tomadas medias exportadas."
                  "\nArquivo: " chosen
                  "\nO desenho nao foi modificado.")))
            (prompt "\nERRO: nao foi possivel criar o arquivo CSV.")))))
  )
  (if file (close file))
  (princ)
)

(prompt
  (strcat
    "\nRotina v" *mcad8:version*
    " carregada. Use MASCARA_V08_STATUS para iniciar."))
(princ)
