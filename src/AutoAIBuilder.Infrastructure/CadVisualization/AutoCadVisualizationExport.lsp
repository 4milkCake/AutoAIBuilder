;;; AutoAIBuilder 11.6E - exportador vetorial somente leitura.
;;; Executado exclusivamente sobre uma copia tecnica do DWG.

(vl-load-com)

(defun aiv:replace-all (text old replacement / position start)
  (setq start 0)
  (while (setq position (vl-string-search old text start))
    (setq text
      (strcat
        (substr text 1 position)
        replacement
        (substr text (+ position (strlen old) 1))))
    ;; Avanca alem do texto inserido para nao recodificar o proprio "%25".
    (setq start (+ position (strlen replacement))))
  text)

(defun aiv:encode (value / text)
  (setq text (if value (vl-princ-to-string value) ""))
  (setq text (aiv:replace-all text "%" "%25"))
  (setq text (aiv:replace-all text "|" "%7C"))
  (setq text (aiv:replace-all text "\r" "%0D"))
  (setq text (aiv:replace-all text "\n" "%0A"))
  text)

(defun aiv:number (value)
  (rtos (if value value 0.0) 2 10))

(defun aiv:degrees (radians)
  (* 180.0 (/ radians pi)))

(defun aiv:world-point (entity point / result)
  (if point
    (progn
      (setq result
        (vl-catch-all-apply 'trans (list point entity 0)))
      (if (vl-catch-all-error-p result) point result))
    '(0.0 0.0 0.0)))

(defun aiv:world-angle (entity radians / direction result)
  (setq direction
    (vl-catch-all-apply
      'trans
      (list
        (list (cos radians) (sin radians) 0.0)
        entity
        0
        T)))
  (if (or (null direction) (vl-catch-all-error-p direction))
    radians
    (progn
      (setq result (atan (cadr direction) (car direction)))
      (if (< result 0.0) (+ result (* 2.0 pi)) result))))

(defun aiv:world-vector-angle (entity vector / direction result)
  (setq direction
    (vl-catch-all-apply
      'trans
      (list vector entity 0 T)))
  (if (or (null direction) (vl-catch-all-error-p direction))
    (setq direction vector))
  (setq result (atan (cadr direction) (car direction)))
  (if (< result 0.0) (+ result (* 2.0 pi)) result))

(defun aiv:world-points (entity points / result point)
  (setq result nil)
  (foreach point points
    (setq result
      (cons (aiv:world-point entity point) result)))
  (reverse result))

(defun aiv:write-line (file fields / result field)
  (setq result "")
  (foreach field fields
    (setq result
      (if (= result "")
        field
        (strcat result "|" field))))
  (write-line result file)
  (if
    (member
      (car fields)
      '("LINE" "POLYLINE" "CIRCLE" "ARC" "TEXT" "BOX"))
    (setq *aiv:exported-count*
      (1+ (if *aiv:exported-count* *aiv:exported-count* 0)))))

(defun aiv:point-pair (point)
  (strcat (aiv:number (car point)) "," (aiv:number (cadr point))))

(defun aiv:curve-points (entity divisions / end distance index point result)
  (setq result nil)
  (setq end (vl-catch-all-apply 'vlax-curve-getEndParam (list entity)))
  (if (not (vl-catch-all-error-p end))
    (progn
      (setq distance
        (vl-catch-all-apply 'vlax-curve-getDistAtParam (list entity end)))
      (if (and (not (vl-catch-all-error-p distance)) (> distance 0.0))
        (progn
          (setq index 0)
          (repeat (+ divisions 1)
            (setq point
              (vl-catch-all-apply
                'vlax-curve-getPointAtDist
                (list entity (* distance (/ index (float divisions))))))
            (if (not (vl-catch-all-error-p point))
              (setq result (cons point result)))
            (setq index (1+ index)))))))
  (reverse result))

(defun aiv:points-text (points / result point)
  (setq result "")
  (foreach point points
    (setq result
      (if (= result "")
        (aiv:point-pair point)
        (strcat result ";" (aiv:point-pair point)))))
  result)

(defun aiv:text-value (data type / result item)
  (setq result "")
  (if (= type "MTEXT")
    (foreach item data
      (if (or (= (car item) 3) (= (car item) 1))
        (setq result (strcat result (cdr item)))))
    (if (cdr (assoc 1 data))
      (setq result (cdr (assoc 1 data)))))
  result)

(defun aiv:text-point (entity data type / horizontal vertical point)
  (setq horizontal (if (cdr (assoc 72 data)) (cdr (assoc 72 data)) 0))
  (setq vertical
    (cond
      ((cdr (assoc 74 data)) (cdr (assoc 74 data)))
      ((cdr (assoc 73 data)) (cdr (assoc 73 data)))
      (T 0)))
  (setq point
    (if (and
          (member type '("TEXT" "ATTRIB"))
          (or (/= horizontal 0) (/= vertical 0))
          (cdr (assoc 11 data)))
      (cdr (assoc 11 data))
      (cdr (assoc 10 data))))
  (aiv:world-point entity point))

(defun aiv:text-attachment (data type / code horizontal vertical hname)
  (if (= type "MTEXT")
    (progn
      (setq code (if (cdr (assoc 71 data)) (cdr (assoc 71 data)) 1))
      (nth
        (max 0 (min 8 (1- code)))
        '("TopLeft" "TopCenter" "TopRight"
          "MiddleLeft" "MiddleCenter" "MiddleRight"
          "BottomLeft" "BottomCenter" "BottomRight")))
    (progn
      (setq horizontal (if (cdr (assoc 72 data)) (cdr (assoc 72 data)) 0))
      (setq vertical
        (cond
          ((cdr (assoc 74 data)) (cdr (assoc 74 data)))
          ((cdr (assoc 73 data)) (cdr (assoc 73 data)))
          (T 0)))
      (setq hname
        (cond
          ((= horizontal 2) "Right")
          ((member horizontal '(1 3 4 5)) "Center")
          (T "Left")))
      (strcat
        (cond
          ((= vertical 3) "Top")
          ((= vertical 2) "Middle")
          ((= vertical 1) "Bottom")
          (T "Baseline"))
        hname))))

(defun aiv:text-rotation (entity data type / rotation direction)
  (setq rotation (cdr (assoc 50 data)))
  (if rotation
    (aiv:world-angle entity rotation)
    (progn
      (setq direction
        (if (= type "MTEXT") (cdr (assoc 11 data)) nil))
      (if direction
        (aiv:world-vector-angle entity direction)
        0.0))))

(defun aiv:bounding-box (entity / object minimum maximum result)
  (setq object
    (vl-catch-all-apply 'vlax-ename->vla-object (list entity)))
  (if (or (null object) (vl-catch-all-error-p object))
    nil
    (progn
      (setq result
        (vl-catch-all-apply
          'vla-GetBoundingBox
          (list object 'minimum 'maximum)))
      (if (vl-catch-all-error-p result)
        nil
        (list
          (vlax-safearray->list minimum)
          (vlax-safearray->list maximum))))))

(defun aiv:write-box (file entity handle layer / box minimum maximum)
  (setq box (aiv:bounding-box entity))
  (if box
    (progn
      (setq minimum (car box))
      (setq maximum (cadr box))
      (aiv:write-line file
        (list
          "BOX" (aiv:encode handle) (aiv:encode layer)
          (aiv:number (car minimum)) (aiv:number (cadr minimum))
          (aiv:number (car maximum)) (aiv:number (cadr maximum)))))))

(setq *aiv:expanded-inserts* nil)
(setq *aiv:failed-inserts* nil)
(setq *aiv:exported-count* 0)

(defun aiv:expand-inserts
  (/ previous-qaflags pass continue selection before-count after-count
     index entity data handle)
  ;; A chamada COM Explode pode retornar NIL sem erro para blocos muito
  ;; grandes. O comando nativo materializa corretamente as entidades no banco
  ;; de dados da copia tecnica e informa quais insercoes nao sao explodiveis.
  (setq previous-qaflags (getvar "QAFLAGS"))
  (setvar "QAFLAGS" 1)
  (command "_.-LAYER" "_Unlock" "*" "_Thaw" "*" "_On" "*" "")
  (setq pass 0)
  (setq continue T)
  (while (and continue (< pass 12))
    (setq selection (ssget "_X" '((0 . "INSERT") (410 . "Model"))))
    (if (null selection)
      (setq continue nil)
      (progn
        (setq before-count (sslength selection))
        (command "_.EXPLODE" selection "")
        (setq selection
          (ssget "_X" '((0 . "INSERT") (410 . "Model"))))
        (setq after-count (if selection (sslength selection) 0))
        (prompt
          (strcat
            "\nAIV_EXPAND_PASS:" (itoa pass)
            ":BEFORE:" (itoa before-count)
            ":AFTER:" (itoa after-count)))
        (if (>= after-count before-count)
          (setq continue nil))))
    (setq pass (1+ pass)))
  (setvar "QAFLAGS" previous-qaflags)

  ;; As poucas insercoes restantes sao representadas pela caixa de limites.
  (setq selection (ssget "_X" '((0 . "INSERT") (410 . "Model"))))
  (if selection
    (progn
      (setq index 0)
      (repeat (sslength selection)
        (setq entity (ssname selection index))
        (setq data (entget entity))
        (setq handle (cdr (assoc 5 data)))
        (setq *aiv:failed-inserts*
          (cons handle *aiv:failed-inserts*))
        (setq index (1+ index))))))

(defun aiv:write-layer-table (file / data name color flags visible)
  (setq data (tblnext "LAYER" T))
  (while data
    (setq name (cdr (assoc 2 data)))
    (setq color (cdr (assoc 62 data)))
    (if (null color) (setq color 7))
    (setq flags (cdr (assoc 70 data)))
    (if (null flags) (setq flags 0))
    (setq visible
      (if (and (> color 0) (= 0 (logand flags 1))) "1" "0"))
    (aiv:write-line file
      (list
        "LAYER"
        (aiv:encode name)
        (itoa (abs color))
        visible))
    (setq data (tblnext "LAYER"))))

(defun aiv:write-entity
  (file entity / data type handle layer point1 point2 center radius
   start-angle end-angle flags points text-value height rotation
   attachment width style)
  (setq data (entget entity))
  (setq type (cdr (assoc 0 data)))
  (setq handle (cdr (assoc 5 data)))
  (setq layer (cdr (assoc 8 data)))
  (cond
    ((= type "LINE")
      (setq point1
        (aiv:world-point entity (cdr (assoc 10 data))))
      (setq point2
        (aiv:world-point entity (cdr (assoc 11 data))))
      (aiv:write-line file
        (list
          "LINE" (aiv:encode handle) (aiv:encode layer)
          (aiv:number (car point1)) (aiv:number (cadr point1))
          (aiv:number (car point2)) (aiv:number (cadr point2)))))
    ((= type "LWPOLYLINE")
      (setq points
        (mapcar 'cdr
          (vl-remove-if-not
            '(lambda (item) (= (car item) 10))
            data)))
      (setq points (aiv:world-points entity points))
      (setq flags (cdr (assoc 70 data)))
      (if (null flags) (setq flags 0))
      (aiv:write-line file
        (list
          "POLYLINE" (aiv:encode handle) (aiv:encode layer)
          (if (= 1 (logand flags 1)) "1" "0")
          (aiv:points-text points))))
    ((= type "POLYLINE")
      (setq points (aiv:curve-points entity 48))
      (setq flags (cdr (assoc 70 data)))
      (if (null flags) (setq flags 0))
      (if points
        (aiv:write-line file
          (list
            "POLYLINE" (aiv:encode handle) (aiv:encode layer)
            (if (= 1 (logand flags 1)) "1" "0")
            (aiv:points-text points)))))
    ((= type "CIRCLE")
      (setq center
        (aiv:world-point entity (cdr (assoc 10 data))))
      (setq radius (cdr (assoc 40 data)))
      (aiv:write-line file
        (list
          "CIRCLE" (aiv:encode handle) (aiv:encode layer)
          (aiv:number (car center)) (aiv:number (cadr center))
          (aiv:number radius))))
    ((= type "ARC")
      (setq center
        (aiv:world-point entity (cdr (assoc 10 data))))
      (setq radius (cdr (assoc 40 data)))
      (setq start-angle
        (aiv:world-angle entity (cdr (assoc 50 data))))
      (setq end-angle
        (aiv:world-angle entity (cdr (assoc 51 data))))
      (aiv:write-line file
        (list
          "ARC" (aiv:encode handle) (aiv:encode layer)
          (aiv:number (car center)) (aiv:number (cadr center))
          (aiv:number radius)
          (aiv:number (aiv:degrees start-angle))
          (aiv:number (aiv:degrees end-angle)))))
    ((or (= type "ELLIPSE") (= type "SPLINE"))
      (setq points (aiv:curve-points entity 48))
      (if points
        (aiv:write-line file
          (list
            "POLYLINE" (aiv:encode handle) (aiv:encode layer)
            (if (= type "ELLIPSE") "1" "0")
            (aiv:points-text points)))))
    ((or (= type "TEXT") (= type "MTEXT") (= type "ATTRIB"))
      (setq point1 (aiv:text-point entity data type))
      (setq height (cdr (assoc 40 data)))
      (setq rotation (aiv:text-rotation entity data type))
      (setq text-value (aiv:text-value data type))
      (setq attachment (aiv:text-attachment data type))
      (setq width
        (if (and (= type "MTEXT") (cdr (assoc 41 data)))
          (cdr (assoc 41 data))
          0.0))
      (setq style (if (cdr (assoc 7 data)) (cdr (assoc 7 data)) ""))
      (if (null height) (setq height 1.0))
      (aiv:write-line file
        (list
          "TEXT" (aiv:encode handle) (aiv:encode layer)
          (aiv:number (car point1)) (aiv:number (cadr point1))
          (aiv:number height)
          (aiv:number (aiv:degrees rotation))
          (aiv:encode text-value)
          attachment
          (aiv:number width)
          (aiv:encode style)
          type)))
    ((= type "INSERT")
      (if (member handle *aiv:failed-inserts*)
        (aiv:write-box file entity handle layer)))
    ((or
       (= type "SOLID") (= type "TRACE") (= type "3DFACE")
       (= type "HATCH") (= type "WIPEOUT") (= type "IMAGE")
       (= type "DIMENSION") (= type "LEADER") (= type "MLEADER"))
      (aiv:write-box file entity handle layer))))

(defun aiv:export
  (output-path / application document file minimum maximum selection index
   source-count remaining-count)
  ;; O Core Console pode nao expor o objeto COM da aplicacao, embora todas as
  ;; funcoes nativas de banco de dados estejam disponiveis. Regenerar melhora
  ;; EXTMIN/EXTMAX, mas nao e requisito para a exportacao e nunca deve impedir
  ;; a leitura segura da copia tecnica.
  (setq application
    (vl-catch-all-apply 'vlax-get-acad-object '()))
  (if (and
        (not (null application))
        (not (vl-catch-all-error-p application)))
    (setq document
      (vl-catch-all-apply 'vla-get-ActiveDocument (list application))))
  (if (and
        (not (null document))
        (not (vl-catch-all-error-p document)))
    (vl-catch-all-apply 'vla-Regen (list document 1)))
  (aiv:expand-inserts)
  (if (and
        (not (null document))
        (not (vl-catch-all-error-p document)))
    (vl-catch-all-apply 'vla-Regen (list document 1)))
  (setq minimum (getvar "EXTMIN"))
  (setq maximum (getvar "EXTMAX"))
  (setq file (open output-path "w" "utf8"))
  (if (null file)
    (progn
      (prompt "\nAIV_ERROR: nao foi possivel criar o artefato.")
      nil)
    (progn
      (aiv:write-line file (list "AIV" "2"))
      (aiv:write-line file
        (list
          "BOUNDS"
          (aiv:number (car minimum)) (aiv:number (cadr minimum))
          (aiv:number (car maximum)) (aiv:number (cadr maximum))))
      (aiv:write-layer-table file)
      (setq selection (ssget "_X" '((410 . "Model"))))
      (setq source-count (if selection (sslength selection) 0))
      (setq *aiv:exported-count* 0)
      (if selection
        (progn
          (setq index 0)
          (repeat (sslength selection)
            (aiv:write-entity file (ssname selection index))
            (setq index (1+ index)))))
      (setq remaining-count
        (if *aiv:failed-inserts* (length *aiv:failed-inserts*) 0))
      (aiv:write-line file
        (list
          "COVERAGE"
          (itoa source-count)
          (itoa *aiv:exported-count*)
          (itoa remaining-count)))
      (close file)
      (prompt (strcat "\nAIV_OK:" output-path))
      T)))

(princ "\nAutoAIBuilder Visual Export 1.0 carregado.")
(princ)
