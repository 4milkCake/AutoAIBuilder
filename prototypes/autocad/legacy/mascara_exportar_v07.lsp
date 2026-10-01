;;; mascara_exportar_v07.lsp
;;; Versao 0.7.0 - auditoria final e exportacao do mapa de coordenadas.
;;; Requer, na mesma sessao do AutoCAD:
;;;   mascara_previsualizar_v04.lsp v0.4.2
;;;   mascara_camadas_v05.lsp v0.5.0
;;;   mascara_limpeza_v06.lsp v0.6.0
;;;
;;; SEGURANCA:
;;; - Nao apaga, move, explode, cria layer ou altera qualquer entidade.
;;; - Somente consulta o estado validado e grava arquivos CSV externos.
;;; - Exige uma copia cujo nome contenha MASCARA ou COPIA.
;;; - Exige todos os objetos semanticos nas layers corretas.
;;; - Exige zero objetos descartaveis presentes.
;;; - Nao le nem modifica o interior de CINZA PONTOS.
;;;
;;; ARQUIVOS GERADOS:
;;;   *_pontos_semanticos_v07.csv
;;;   *_componentes_semanticos_v07.csv
;;;   *_auditoria_mascara_v07.csv
;;;
;;; COMANDOS:
;;;   MASCARA_V07_PREPARAR       - confere o estado atual sem refazer a legenda
;;;   MASCARA_V07_STATUS         - apresenta a auditoria final no AutoCAD
;;;   MASCARA_V07_PREVISUALIZAR  - destaca os pontos e componentes exportados
;;;   MASCARA_V07_EXPORTAR       - grava os tres CSVs

(vl-load-com)

(setq *mcad7:last-files* nil)
(setq *mcad7:last-summary* nil)

(defun mcad7:dependencies-loaded-p ()
  (and
    (boundp '*mcad4:legend-selection*)
    (boundp '*mcad4:items*)
    (boundp '*mcad4:components*)
    (boundp '*mcad5:changes*)
    (boundp '*mcad5:created-layers*)
    (boundp '*mcad6:deleted*))
)

(defun mcad7:ready-p ()
  (and
    (mcad7:dependencies-loaded-p)
    (mcad6:ready-p))
)

(defun mcad7:safe-copy-p ()
  (and
    (mcad7:dependencies-loaded-p)
    (mcad6:safe-copy-p))
)

(defun mcad7:yes-no (value)
  (if value "SIM" "NAO")
)

(defun mcad7:status-word (condition)
  (if condition "OK" "BLOQUEIO")
)

(defun mcad7:unit-name (code)
  (cond
    ((= code 0) "SEM_UNIDADE")
    ((= code 1) "POLEGADAS")
    ((= code 2) "PES")
    ((= code 3) "MILHAS")
    ((= code 4) "MILIMETROS")
    ((= code 5) "CENTIMETROS")
    ((= code 6) "METROS")
    ((= code 7) "QUILOMETROS")
    ((= code 8) "MICROPOLEGADAS")
    ((= code 9) "MILS")
    ((= code 10) "JARDAS")
    ((= code 11) "ANGSTROMS")
    ((= code 12) "NANOMETROS")
    ((= code 13) "MICRONS")
    ((= code 14) "DECIMETROS")
    ((= code 15) "DECAMETROS")
    ((= code 16) "HECTOMETROS")
    ((= code 17) "GIGAMETROS")
    ((= code 18) "UNIDADES_ASTRONOMICAS")
    ((= code 19) "ANOS_LUZ")
    ((= code 20) "PARSECS")
    (T "CODIGO_NAO_MAPEADO"))
)

(defun mcad7:discipline-prefix (discipline)
  (cond
    ((= discipline "ELETRICO") "ELE")
    ((= discipline "HIDRAULICO") "HID")
    (T "PTO"))
)

(defun mcad7:point-id (discipline handle)
  (strcat (mcad7:discipline-prefix discipline) "-" handle)
)

(defun mcad7:replace-all (text old new / position)
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

(defun mcad7:clean-text (text / result index character code)
  (setq result "")
  (setq text
    (mcad7:replace-all
      (mcad7:replace-all (if text text "") "\\P" " ")
      "\\~" " "))
  (setq index 1)
  (while (<= index (strlen text))
    (setq character (substr text index 1))
    (setq code (ascii character))
    (if (or (= code 9) (= code 10) (= code 13))
      (setq character " "))
    (setq result (strcat result character))
    (setq index (1+ index)))
  result
)

(defun mcad7:join (values separator / result value)
  (setq result "")
  (foreach value values
    (setq result
      (strcat result (if (= result "") "" separator) value)))
  result
)

(defun mcad7:value (data code default / pair)
  (if (setq pair (assoc code data))
    (cdr pair)
    default)
)

(defun mcad7:entity-point (data box / point)
  (setq point (cdr (assoc 10 data)))
  (if point
    (list
      (mcad3:point-value point 0)
      (mcad3:point-value point 1)
      (mcad3:point-value point 2))
    (list (nth 6 box) (nth 7 box) (nth 8 box)))
)

(defun mcad7:safe-box (entity data / result)
  (setq result
    (vl-catch-all-apply 'mcad32:box (list entity data)))
  (if (vl-catch-all-error-p result)
    (mcad32:fallback-box data)
    result)
)

(defun mcad7:block-name (entity data / result)
  (setq result
    (vl-catch-all-apply 'mcad33:block-name (list entity data)))
  (if (vl-catch-all-error-p result) "" result)
)

(defun mcad7:rotation-degrees (data / rotation)
  (setq rotation (mcad7:value data 50 0.0))
  (* rotation (/ 180.0 pi))
)

(defun mcad7:component-kind (component)
  (if (= (nth 2 component) "REF_ELE_ANOTACAO")
    "TEXTO"
    "GRAFICO")
)

(defun mcad7:components-for-seed (seed-handle / result component)
  (setq result nil)
  (foreach component *mcad4:components*
    (if (= (nth 4 component) seed-handle)
      (setq result (cons component result))))
  (reverse result)
)

;;; Retorno: (QTD_GRAFICOS QTD_TEXTOS LISTA_TEXTOS)
(defun mcad7:component-summary
  (seed-handle / components graphics texts values component entity data content)
  (setq components (mcad7:components-for-seed seed-handle))
  (setq graphics 0 texts 0 values nil)
  (foreach component components
    (if (= (mcad7:component-kind component) "TEXTO")
      (progn
        (setq texts (1+ texts))
        (setq entity (nth 0 component))
        (setq data (entget entity))
        (if data
          (progn
            (setq content
              (mcad7:clean-text (mcad32:text-content data)))
            (if (/= content "")
              (setq values (cons content values))))))
      (setq graphics (1+ graphics))))
  (list graphics texts (mcad7:join (reverse values) " | "))
)

;;; Retorno da auditoria:
;;; (PLAN ORGANIZATION DISCARD BASE LAYERS POINTS ELE HID COMPONENTS SAFE)
(defun mcad7:audit
  (/ plan organization discard base layers points electric hydraulic
     components safe entry)
  (setq plan (mcad5:point-plan))
  (setq organization (mcad6:organization-state))
  (setq discard (sslength (mcad6:discard-selection)))
  (setq base (mcad6:base-count))
  (setq layers (mcad5:unique-layers plan))
  (setq points (length plan))
  (setq electric 0 hydraulic 0)
  (foreach entry plan
    (cond
      ((= (nth 2 entry) "ELETRICO")
        (setq electric (1+ electric)))
      ((= (nth 2 entry) "HIDRAULICO")
        (setq hydraulic (1+ hydraulic)))))
  (setq components (mcad5:associated-component-count plan))
  (setq safe (mcad7:safe-copy-p))
  (list plan organization discard base layers points electric hydraulic
        components safe)
)

(defun mcad7:audit-ok-p (audit / organization)
  (setq organization (nth 1 audit))
  (and
    (nth 9 audit)
    (> (nth 5 audit) 0)
    (= (nth 0 organization) (nth 1 organization))
    (= (nth 0 organization) (+ (nth 5 audit) (nth 8 audit)))
    (= (nth 2 audit) 0)
    (> (nth 3 audit) 0))
)

(defun mcad7:reference-counts-p (audit)
  (and
    (= (nth 5 audit) 272)
    (= (nth 6 audit) 196)
    (= (nth 7 audit) 76)
    (= (nth 8 audit) 100)
    (= (length (nth 4 audit)) 35)
    (= (nth 3 audit) 3))
)

(defun mcad7:print-audit (audit / organization total)
  (setq organization (nth 1 audit))
  (setq total (+ (nth 5 audit) (nth 8 audit)))
  (prompt "\nAuditoria final da mascara v0.7:")
  (prompt
    (strcat
      "\nObjetos semanticos: "
      (itoa (nth 1 organization)) "/"
      (itoa (nth 0 organization))
      " organizados."))
  (prompt
    (strcat
      "\nPontos principais: " (itoa (nth 5 audit))
      " | ELE=" (itoa (nth 6 audit))
      " | HID=" (itoa (nth 7 audit))))
  (prompt
    (strcat
      "\nComponentes/textos associados: " (itoa (nth 8 audit))
      " | total exportavel: " (itoa total)))
  (prompt
    (strcat
      "\nLayers semanticas: " (itoa (length (nth 4 audit)))
      " | descartaveis presentes: " (itoa (nth 2 audit))
      " | base/contexto: " (itoa (nth 3 audit))))
  (prompt
    (strcat
      "\nUnidade das coordenadas (INSUNITS): "
      (itoa (getvar "INSUNITS")) " - "
      (mcad7:unit-name (getvar "INSUNITS"))))
  (if (nth 9 audit)
    (prompt "\nNome do DWG autorizado.")
    (prompt
      "\nBLOQUEIO: use uma copia com MASCARA ou COPIA no nome."))
  (if (mcad7:reference-counts-p audit)
    (prompt
      "\nContagens iguais a referencia validada: 272 pontos, 100 associados e 35 layers.")
    (prompt
      "\nATENCAO: as contagens diferem do desenho TESTE_01, mas a consistencia interna sera verificada."))
  (if (mcad7:audit-ok-p audit)
    (prompt
      "\nAUDITORIA APROVADA. A exportacao pode ser executada.")
    (prompt
      "\nAUDITORIA BLOQUEADA. Corrija os itens acima antes de exportar."))
  (prompt
    "\nCINZA PONTOS continua intacto e seu interior nao foi lido.")
)

(defun mcad7:ends-with-p (text suffix / text-length suffix-length)
  (setq text (if text text ""))
  (setq suffix (if suffix suffix ""))
  (setq text-length (strlen text))
  (setq suffix-length (strlen suffix))
  (and
    (>= text-length suffix-length)
    (= (strcase (substr text (1+ (- text-length suffix-length))))
       (strcase suffix)))
)

(defun mcad7:strip-suffix (text suffix)
  (if (mcad7:ends-with-p text suffix)
    (substr text 1 (- (strlen text) (strlen suffix)))
    text)
)

(defun mcad7:output-paths
  (chosen / directory file-base root-base root points components audit)
  (setq directory (vl-filename-directory chosen))
  (setq file-base (vl-filename-base chosen))
  (setq root-base
    (mcad7:strip-suffix file-base "_pontos_semanticos_v07"))
  (setq root (strcat directory "\\" root-base))
  (setq points
    (if (mcad7:ends-with-p file-base "_pontos_semanticos_v07")
      chosen
      (strcat root "_pontos_semanticos_v07.csv")))
  (setq components (strcat root "_componentes_semanticos_v07.csv"))
  (setq audit (strcat root "_auditoria_mascara_v07.csv"))
  (list points components audit)
)

(defun mcad7:any-file-exists-p (paths / found path)
  (setq found nil)
  (foreach path paths
    (if (findfile path) (setq found T)))
  found
)

(defun mcad7:write-point-header (file)
  (write-line
    "\"ARQUIVO_DWG\";\"CAMINHO_DWG\";\"ID_PONTO\";\"HANDLE_PONTO\";\"DISCIPLINA\";\"CODIGO_SEMANTICO\";\"DESCRICAO\";\"ALTURA_CM\";\"CONFIANCA\";\"FONTE\";\"LAYER_SEMANTICA\";\"NOME_BLOCO\";\"X_INSERCAO\";\"Y_INSERCAO\";\"Z_INSERCAO\";\"X_CENTRO\";\"Y_CENTRO\";\"Z_CENTRO\";\"ROTACAO_GRAUS\";\"ESCALA_X\";\"ESCALA_Y\";\"ESCALA_Z\";\"QTD_COMPONENTES_GRAFICOS\";\"QTD_TEXTOS_ASSOCIADOS\";\"TEXTOS_ASSOCIADOS\";\"INSUNITS_CODIGO\";\"UNIDADE_COORDENADAS\""
    file)
)

(defun mcad7:write-component-header (file)
  (write-line
    "\"ARQUIVO_DWG\";\"CAMINHO_DWG\";\"ID_COMPONENTE\";\"HANDLE_COMPONENTE\";\"ID_PONTO\";\"HANDLE_PONTO\";\"DISCIPLINA\";\"CODIGO_PONTO\";\"LAYER_SEMANTICA\";\"CLASSE_COMPONENTE\";\"CATEGORIA_ORIGEM\";\"TIPO_OBJETO\";\"NOME_BLOCO\";\"CONTEUDO_TEXTO\";\"DISTANCIA_ASSOCIACAO\";\"CONFIANCA_ASSOCIACAO\";\"X_REFERENCIA\";\"Y_REFERENCIA\";\"Z_REFERENCIA\";\"X_CENTRO\";\"Y_CENTRO\";\"Z_CENTRO\";\"ROTACAO_GRAUS\""
    file)
)

(defun mcad7:write-audit-header (file)
  (write-line
    "\"CHAVE\";\"VALOR\";\"STATUS\";\"OBSERVACAO\""
    file)
)

(defun mcad7:write-audit-row (file key value status observation)
  (write-line
    (strcat
      (mcad3:csv-field key) ";"
      (mcad3:csv-field value) ";"
      (mcad3:csv-field status) ";"
      (mcad3:csv-field observation))
    file)
)

(defun mcad7:write-point-row
  (file drawing-name drawing-full-path entry
    / entity handle layer discipline info data box insertion block-name
      summary unit-code)
  (setq handle (nth 0 entry))
  (setq layer (nth 1 entry))
  (setq discipline (nth 2 entry))
  (setq entity (nth 3 entry))
  (setq info (nth 4 entry))
  (setq data (entget entity))
  (if data
    (progn
      (setq box (mcad7:safe-box entity data))
      (setq insertion (mcad7:entity-point data box))
      (setq block-name (mcad7:block-name entity data))
      (setq summary (mcad7:component-summary handle))
      (setq unit-code (getvar "INSUNITS"))
      (write-line
        (strcat
          (mcad3:csv-field drawing-name) ";"
          (mcad3:csv-field drawing-full-path) ";"
          (mcad3:csv-field (mcad7:point-id discipline handle)) ";"
          (mcad3:csv-field handle) ";"
          (mcad3:csv-field discipline) ";"
          (mcad3:csv-field (nth 3 info)) ";"
          (mcad3:csv-field (nth 4 info)) ";"
          (mcad3:csv-field (nth 5 info)) ";"
          (mcad3:csv-field (nth 7 info)) ";"
          (mcad3:csv-field (nth 8 info)) ";"
          (mcad3:csv-field layer) ";"
          (mcad3:csv-field block-name) ";"
          (mcad3:csv-field
            (mcad3:number-text (mcad3:point-value insertion 0))) ";"
          (mcad3:csv-field
            (mcad3:number-text (mcad3:point-value insertion 1))) ";"
          (mcad3:csv-field
            (mcad3:number-text (mcad3:point-value insertion 2))) ";"
          (mcad3:csv-field (mcad3:number-text (nth 6 box))) ";"
          (mcad3:csv-field (mcad3:number-text (nth 7 box))) ";"
          (mcad3:csv-field (mcad3:number-text (nth 8 box))) ";"
          (mcad3:csv-field
            (mcad3:number-text (mcad7:rotation-degrees data))) ";"
          (mcad3:csv-field
            (mcad3:number-text (mcad7:value data 41 1.0))) ";"
          (mcad3:csv-field
            (mcad3:number-text (mcad7:value data 42 1.0))) ";"
          (mcad3:csv-field
            (mcad3:number-text (mcad7:value data 43 1.0))) ";"
          (mcad3:csv-field (itoa (nth 0 summary))) ";"
          (mcad3:csv-field (itoa (nth 1 summary))) ";"
          (mcad3:csv-field (nth 2 summary)) ";"
          (mcad3:csv-field (itoa unit-code)) ";"
          (mcad3:csv-field (mcad7:unit-name unit-code)))
        file)
      T)
    nil)
)

(defun mcad7:write-component-row
  (file drawing-name drawing-full-path component plan
    / entity handle seed-handle seed-entry discipline info layer data box
      reference-point entity-type block-name content)
  (setq entity (nth 0 component))
  (setq handle (nth 1 component))
  (setq seed-handle (nth 4 component))
  (setq seed-entry (assoc seed-handle plan))
  (setq data (entget entity))
  (if (and seed-entry data)
    (progn
      (setq discipline (nth 2 seed-entry))
      (setq info (nth 4 seed-entry))
      (setq layer (nth 1 seed-entry))
      (setq box (mcad7:safe-box entity data))
      (setq reference-point (mcad7:entity-point data box))
      (setq entity-type (mcad7:value data 0 ""))
      (setq block-name (mcad7:block-name entity data))
      (setq content
        (if (or (= entity-type "TEXT")
                (= entity-type "MTEXT")
                (= entity-type "ATTRIB")
                (= entity-type "ATTDEF"))
          (mcad7:clean-text (mcad32:text-content data))
          ""))
      (write-line
        (strcat
          (mcad3:csv-field drawing-name) ";"
          (mcad3:csv-field drawing-full-path) ";"
          (mcad3:csv-field (strcat "CMP-" handle)) ";"
          (mcad3:csv-field handle) ";"
          (mcad3:csv-field
            (mcad7:point-id discipline seed-handle)) ";"
          (mcad3:csv-field seed-handle) ";"
          (mcad3:csv-field discipline) ";"
          (mcad3:csv-field (nth 3 info)) ";"
          (mcad3:csv-field layer) ";"
          (mcad3:csv-field (mcad7:component-kind component)) ";"
          (mcad3:csv-field (nth 2 component)) ";"
          (mcad3:csv-field entity-type) ";"
          (mcad3:csv-field block-name) ";"
          (mcad3:csv-field content) ";"
          (mcad3:csv-field
            (mcad3:number-text (nth 5 component))) ";"
          (mcad3:csv-field (nth 6 component)) ";"
          (mcad3:csv-field
            (mcad3:number-text
              (mcad3:point-value reference-point 0))) ";"
          (mcad3:csv-field
            (mcad3:number-text
              (mcad3:point-value reference-point 1))) ";"
          (mcad3:csv-field
            (mcad3:number-text
              (mcad3:point-value reference-point 2))) ";"
          (mcad3:csv-field (mcad3:number-text (nth 6 box))) ";"
          (mcad3:csv-field (mcad3:number-text (nth 7 box))) ";"
          (mcad3:csv-field (mcad3:number-text (nth 8 box))) ";"
          (mcad3:csv-field
            (mcad3:number-text (mcad7:rotation-degrees data))))
        file)
      T)
    nil)
)

(defun mcad7:layer-point-count (layer-name plan / count entry)
  (setq count 0)
  (foreach entry plan
    (if (= (strcase (nth 1 entry)) (strcase layer-name))
      (setq count (1+ count))))
  count
)

(defun mcad7:layer-component-count
  (layer-name plan / count component seed-entry)
  (setq count 0)
  (foreach component *mcad4:components*
    (setq seed-entry (assoc (nth 4 component) plan))
    (if (and seed-entry
             (= (strcase (nth 1 seed-entry)) (strcase layer-name)))
      (setq count (1+ count))))
  count
)

(defun mcad7:write-audit
  (file audit paths exported-points exported-components
    / plan organization layer-entry layer-name point-count component-count)
  (setq plan (nth 0 audit))
  (setq organization (nth 1 audit))
  (mcad7:write-audit-row file "VERSAO_ROTINA" "0.7.0" "INFO"
    "Auditoria final e mapa de coordenadas")
  (mcad7:write-audit-row file "ARQUIVO_DWG" (getvar "DWGNAME") "INFO"
    (strcat (getvar "DWGPREFIX") (getvar "DWGNAME")))
  (mcad7:write-audit-row file "NOME_COPIA_AUTORIZADO"
    (mcad7:yes-no (nth 9 audit))
    (mcad7:status-word (nth 9 audit))
    "O nome deve conter MASCARA ou COPIA")
  (mcad7:write-audit-row file "INSUNITS_CODIGO"
    (itoa (getvar "INSUNITS")) "INFO"
    (mcad7:unit-name (getvar "INSUNITS")))
  (mcad7:write-audit-row file "OBJETOS_SEMANTICOS_ESPERADOS"
    (itoa (nth 0 organization)) "INFO"
    "Pontos principais mais componentes e textos associados")
  (mcad7:write-audit-row file "OBJETOS_SEMANTICOS_ORGANIZADOS"
    (itoa (nth 1 organization))
    (mcad7:status-word
      (= (nth 0 organization) (nth 1 organization)))
    "Todos devem estar na layer semantica do ponto")
  (mcad7:write-audit-row file "PONTOS_PRINCIPAIS"
    (itoa (nth 5 audit))
    (if (= (nth 5 audit) 272) "REFERENCIA_OK" "CONSISTENCIA_INTERNA")
    "Referencia do TESTE_01: 272")
  (mcad7:write-audit-row file "PONTOS_ELETRICOS"
    (itoa (nth 6 audit))
    (if (= (nth 6 audit) 196) "REFERENCIA_OK" "CONSISTENCIA_INTERNA")
    "Referencia do TESTE_01: 196")
  (mcad7:write-audit-row file "PONTOS_HIDRAULICOS"
    (itoa (nth 7 audit))
    (if (= (nth 7 audit) 76) "REFERENCIA_OK" "CONSISTENCIA_INTERNA")
    "Referencia do TESTE_01: 76")
  (mcad7:write-audit-row file "COMPONENTES_TEXTOS_ASSOCIADOS"
    (itoa (nth 8 audit))
    (if (= (nth 8 audit) 100) "REFERENCIA_OK" "CONSISTENCIA_INTERNA")
    "Referencia do TESTE_01: 100")
  (mcad7:write-audit-row file "LAYERS_SEMANTICAS"
    (itoa (length (nth 4 audit)))
    (if (= (length (nth 4 audit)) 35)
      "REFERENCIA_OK" "CONSISTENCIA_INTERNA")
    "Referencia do TESTE_01: 35")
  (mcad7:write-audit-row file "DESCARTAVEIS_PRESENTES"
    (itoa (nth 2 audit))
    (mcad7:status-word (= (nth 2 audit) 0))
    "A exportacao exige zero")
  (mcad7:write-audit-row file "BASE_CONTEXTO_PRESENTES"
    (itoa (nth 3 audit))
    (if (= (nth 3 audit) 3) "REFERENCIA_OK" "ATENCAO")
    "Referencia do TESTE_01: 3")
  (mcad7:write-audit-row file "CINZA_PONTOS"
    "INTACTO_NAO_LIDO" "OK"
    "O interior do bloco nao foi analisado nem modificado")
  (mcad7:write-audit-row file "LINHAS_PONTOS_EXPORTADAS"
    (itoa exported-points)
    (mcad7:status-word (= exported-points (nth 5 audit)))
    (nth 0 paths))
  (mcad7:write-audit-row file "LINHAS_COMPONENTES_EXPORTADAS"
    (itoa exported-components)
    (mcad7:status-word (= exported-components (nth 8 audit)))
    (nth 1 paths))
  (mcad7:write-audit-row file "AUDITORIA_GERAL"
    (if (mcad7:audit-ok-p audit) "APROVADA" "BLOQUEADA")
    (mcad7:status-word (mcad7:audit-ok-p audit))
    "Nenhuma entidade do DWG foi alterada pela v0.7")

  (foreach layer-entry (reverse (nth 4 audit))
    (setq layer-name (nth 0 layer-entry))
    (setq point-count (mcad7:layer-point-count layer-name plan))
    (setq component-count
      (mcad7:layer-component-count layer-name plan))
    (mcad7:write-audit-row file
      "LAYER_SEMANTICA"
      layer-name
      "OK"
      (strcat
        "DISCIPLINA=" (nth 1 layer-entry)
        " | PONTOS=" (itoa point-count)
        " | COMPONENTES_TEXTOS=" (itoa component-count))))
)

(defun c:MASCARA_V07_PREPARAR ()
  (if (not (mcad7:ready-p))
    (prompt
      "\nBLOQUEADO: carregue v0.4.2, v0.5.0 e v0.6.0 na mesma sessao ja preparada.")
    (progn
      (prompt
        "\nA v0.7 usara o estado atual; a legenda nao sera solicitada novamente.")
      (mcad7:print-audit (mcad7:audit))))
  (princ)
)

(defun c:MASCARA_V07_STATUS ()
  (if (not (mcad7:ready-p))
    (prompt
      "\nBLOQUEADO: esta exportacao deve ocorrer na mesma sessao preparada pelas v0.4-v0.6.")
    (mcad7:print-audit (mcad7:audit)))
  (princ)
)

(defun c:MASCARA_V07_PREVISUALIZAR ()
  (if (not (mcad7:ready-p))
    (prompt
      "\nBLOQUEADO: carregue e prepare primeiro as rotinas v0.4-v0.6.")
    (if (not (mcad7:audit-ok-p (mcad7:audit)))
      (prompt
        "\nBLOQUEADO: execute MASCARA_V07_STATUS e corrija a auditoria.")
      (c:MASCARA_V04_PONTOS)))
  (princ)
)

(defun c:MASCARA_V07_EXPORTAR
  (/ *error* old-error point-file component-file audit-file chosen paths
     overwrite-answer audit plan drawing-name drawing-full-path entry
     component exported-points exported-components skipped-points
     skipped-components)

  (setq old-error *error*)

  (defun *error* (message)
    (if point-file (progn (close point-file) (setq point-file nil)))
    (if component-file
      (progn (close component-file) (setq component-file nil)))
    (if audit-file (progn (close audit-file) (setq audit-file nil)))
    (setq *error* old-error)
    (if (and message
             (/= message "Function cancelled")
             (/= message "quit / exit abort"))
      (prompt
        (strcat "\nErro em MASCARA_V07_EXPORTAR: " message)))
    (prompt
      "\nO DWG nao foi alterado. Se algum CSV parcial foi criado, descarte-o e repita.")
    (princ))

  (cond
    ((not (mcad7:dependencies-loaded-p))
      (prompt
        "\nBLOQUEADO: carregue v0.4.2, v0.5.0 e v0.6.0 antes da v0.7."))
    ((not (mcad7:ready-p))
      (prompt
        "\nBLOQUEADO: use a mesma sessao em que a legenda foi preparada e a limpeza concluida."))
    (T
      (setq audit (mcad7:audit))
      (if (not (mcad7:audit-ok-p audit))
        (progn
          (mcad7:print-audit audit)
          (prompt
            "\nExportacao cancelada porque a auditoria nao foi aprovada."))
        (progn
          (setq chosen
            (getfiled
              "Escolha onde salvar o mapa de pontos v0.7"
              (strcat
                (getvar "DWGPREFIX")
                (mcad3:base-name (getvar "DWGNAME"))
                "_pontos_semanticos_v07.csv")
              "csv" 1))
          (if (null chosen)
            (prompt "\nExportacao cancelada. Nenhum arquivo foi criado.")
            (progn
              (setq paths (mcad7:output-paths chosen))
              (setq overwrite-answer "SUBSTITUIR")
              (if (mcad7:any-file-exists-p paths)
                (setq overwrite-answer
                  (strcase
                    (getstring T
                      "\nJa existe pelo menos um CSV. Digite SUBSTITUIR para sobrescrever: "))))
              (if (/= overwrite-answer "SUBSTITUIR")
                (prompt
                  "\nExportacao cancelada. Os arquivos existentes foram preservados.")
                (progn
                  (setq point-file (open (nth 0 paths) "w"))
                  (setq component-file (open (nth 1 paths) "w"))
                  (setq audit-file (open (nth 2 paths) "w"))
                  (if (not (and point-file component-file audit-file))
                    (progn
                      (if point-file
                        (progn (close point-file) (setq point-file nil)))
                      (if component-file
                        (progn
                          (close component-file)
                          (setq component-file nil)))
                      (if audit-file
                        (progn (close audit-file) (setq audit-file nil)))
                      (prompt
                        "\nNao foi possivel criar os tres CSVs. Verifique a pasta e tente novamente."))
                    (progn
                      (mcad7:write-point-header point-file)
                      (mcad7:write-component-header component-file)
                      (mcad7:write-audit-header audit-file)
                      (setq plan (nth 0 audit))
                      (setq drawing-name (getvar "DWGNAME"))
                      (setq drawing-full-path
                        (strcat (getvar "DWGPREFIX") drawing-name))
                      (setq exported-points 0 skipped-points 0)
                      (foreach entry (reverse plan)
                        (if
                          (mcad7:write-point-row
                            point-file drawing-name drawing-full-path entry)
                          (setq exported-points (1+ exported-points))
                          (setq skipped-points (1+ skipped-points))))
                      (setq exported-components 0 skipped-components 0)
                      (foreach component (reverse *mcad4:components*)
                        (if
                          (mcad7:write-component-row
                            component-file drawing-name drawing-full-path
                            component plan)
                          (setq exported-components
                            (1+ exported-components))
                          (setq skipped-components
                            (1+ skipped-components))))
                      (mcad7:write-audit
                        audit-file audit paths
                        exported-points exported-components)
                      (close point-file) (setq point-file nil)
                      (close component-file) (setq component-file nil)
                      (close audit-file) (setq audit-file nil)
                      (setq *mcad7:last-files* paths)
                      (setq *mcad7:last-summary*
                        (list exported-points exported-components
                              skipped-points skipped-components))
                      (prompt "\nExportacao v0.7 concluida.")
                      (prompt
                        (strcat
                          "\nPontos exportados: "
                          (itoa exported-points)
                          " | ignorados: " (itoa skipped-points)))
                      (prompt
                        (strcat
                          "\nComponentes/textos exportados: "
                          (itoa exported-components)
                          " | ignorados: "
                          (itoa skipped-components)))
                      (prompt
                        (strcat "\nMapa de pontos: " (nth 0 paths)))
                      (prompt
                        (strcat "\nComponentes: " (nth 1 paths)))
                      (prompt
                        (strcat "\nAuditoria: " (nth 2 paths)))
                      (prompt
                        "\nNenhuma entidade do DWG foi alterada.")))))))))))

  (setq *error* old-error)
  (princ)
)

(prompt
  "\nRotina v0.7.0 carregada. Use MASCARA_V07_STATUS e depois MASCARA_V07_EXPORTAR."
)
(princ)
