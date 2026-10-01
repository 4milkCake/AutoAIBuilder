;;; AutoAIBuilder 11.6I - inventario CAD somente leitura.
;;; Executado exclusivamente sobre uma copia tecnica do DWG.

(vl-load-com)

(defun aar:replace-all (text old replacement / position start)
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

(defun aar:encode (value / text)
  (setq text (if value (vl-princ-to-string value) ""))
  (setq text (aar:replace-all text "%" "%25"))
  (setq text (aar:replace-all text "|" "%7C"))
  (setq text (aar:replace-all text "\r" "%0D"))
  (setq text (aar:replace-all text "\n" "%0A"))
  text)

(defun aar:number (value)
  (rtos (if value value 0.0) 2 10))

(defun aar:degrees (radians)
  (* 180.0 (/ radians pi)))

(defun aar:world-point (entity point / result)
  (if point
    (progn
      (setq result
        (vl-catch-all-apply 'trans (list point entity 0)))
      (if (vl-catch-all-error-p result) point result))
    '(0.0 0.0 0.0)))

(defun aar:world-rotation (entity radians / direction result)
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

(defun aar:object-bounding-box (object / minimum maximum)
  (vla-GetBoundingBox object 'minimum 'maximum)
  (list
    (vlax-safearray->list minimum)
    (vlax-safearray->list maximum)))

(defun aar:bounding-box (entity / object result)
  (setq object
    (vl-catch-all-apply 'vlax-ename->vla-object (list entity)))
  (if (or (null object) (vl-catch-all-error-p object))
    nil
    (progn
      (setq result
        (vl-catch-all-apply
          'aar:object-bounding-box
          (list object)))
      (if (vl-catch-all-error-p result)
        nil
        result))))

(defun aar:fallback-box (entity data / local minimum maximum)
  (setq local (aar:add-entity-points nil data))
  (if local
    (progn
      (setq minimum (aar:world-point entity (car local)))
      (setq maximum (aar:world-point entity (cadr local)))
      (list
        (list
          (min (car minimum) (car maximum))
          (min (cadr minimum) (cadr maximum))
          (min (caddr minimum) (caddr maximum)))
        (list
          (max (car minimum) (car maximum))
          (max (cadr minimum) (cadr maximum))
          (max (caddr minimum) (caddr maximum)))))
    nil))

(setq *aar:block-bounds-cache* nil)

(defun aar:point3 (point)
  (list
    (if point (car point) 0.0)
    (if (and point (cadr point)) (cadr point) 0.0)
    (if (and point (caddr point)) (caddr point) 0.0)))

(defun aar:add-bound-point (bounds point / value minimum maximum)
  (setq value (aar:point3 point))
  (if bounds
    (progn
      (setq minimum (car bounds))
      (setq maximum (cadr bounds))
      (list
        (list
          (min (car minimum) (car value))
          (min (cadr minimum) (cadr value))
          (min (caddr minimum) (caddr value)))
        (list
          (max (car maximum) (car value))
          (max (cadr maximum) (cadr value))
          (max (caddr maximum) (caddr value)))))
    (list value value)))

(defun aar:add-entity-points
  (bounds data / type item point center radius)
  (setq type (cdr (assoc 0 data)))
  (foreach item data
    (if (and
          (>= (car item) 10)
          (<= (car item) 18)
          (listp (cdr item)))
      (setq bounds (aar:add-bound-point bounds (cdr item)))))
  (if (member type '("CIRCLE" "ARC"))
    (progn
      (setq center (aar:point3 (cdr (assoc 10 data))))
      (setq radius (cdr (assoc 40 data)))
      (if radius
        (progn
          (setq bounds
            (aar:add-bound-point bounds
              (list
                (- (car center) radius)
                (- (cadr center) radius)
                (caddr center))))
          (setq bounds
            (aar:add-bound-point bounds
              (list
                (+ (car center) radius)
                (+ (cadr center) radius)
                (caddr center))))))))
  bounds)

(defun aar:block-local-bounds
  (name / cached block entity data bounds)
  (setq cached (assoc name *aar:block-bounds-cache*))
  (if cached
    (cdr cached)
    (progn
      (setq block (tblobjname "BLOCK" name))
      (setq bounds nil)
      (if block
        (progn
          (setq entity (entnext block))
          (while entity
            (setq data (entget entity))
            (if (= (cdr (assoc 0 data)) "ENDBLK")
              (setq entity nil)
              (progn
                (setq bounds (aar:add-entity-points bounds data))
                (setq entity (entnext entity)))))))
      (setq *aar:block-bounds-cache*
        (cons (cons name bounds) *aar:block-bounds-cache*))
      bounds)))

(defun aar:insert-local-to-world
  (entity data block-base point / insertion rotation sx sy sz
   delta rotated world-delta)
  (setq insertion
    (aar:world-point entity (cdr (assoc 10 data))))
  (setq rotation (if (cdr (assoc 50 data)) (cdr (assoc 50 data)) 0.0))
  (setq sx (if (cdr (assoc 41 data)) (cdr (assoc 41 data)) 1.0))
  (setq sy (if (cdr (assoc 42 data)) (cdr (assoc 42 data)) 1.0))
  (setq sz (if (cdr (assoc 43 data)) (cdr (assoc 43 data)) 1.0))
  (setq delta
    (list
      (* (- (car point) (car block-base)) sx)
      (* (- (cadr point) (cadr block-base)) sy)
      (* (- (caddr point) (caddr block-base)) sz)))
  (setq rotated
    (list
      (-
        (* (car delta) (cos rotation))
        (* (cadr delta) (sin rotation)))
      (+
        (* (car delta) (sin rotation))
        (* (cadr delta) (cos rotation)))
      (caddr delta)))
  (setq world-delta
    (vl-catch-all-apply
      'trans
      (list rotated entity 0 T)))
  (if (vl-catch-all-error-p world-delta)
    (setq world-delta rotated))
  (list
    (+ (car insertion) (car world-delta))
    (+ (cadr insertion) (cadr world-delta))
    (+ (caddr insertion) (caddr world-delta))))

(defun aar:insert-geometry-box
  (entity data / name local block block-base minimum maximum
   xmin ymin zmin xmax ymax zmax corners bounds point)
  ;; Usa a definicao real (assoc 2), inclusive para blocos dinamicos
  ;; anonimos. Assim a caixa e calculada no mesmo WCS usado pelo desenho.
  (setq name (cdr (assoc 2 data)))
  (setq local (if name (aar:block-local-bounds name) nil))
  (setq block (if name (tblobjname "BLOCK" name) nil))
  (if (and local block)
    (progn
      (setq block-base (aar:point3 (cdr (assoc 10 (entget block)))))
      (setq minimum (car local))
      (setq maximum (cadr local))
      (setq xmin (car minimum))
      (setq ymin (cadr minimum))
      (setq zmin (caddr minimum))
      (setq xmax (car maximum))
      (setq ymax (cadr maximum))
      (setq zmax (caddr maximum))
      (setq corners
        (list
          (list xmin ymin zmin)
          (list xmin ymax zmin)
          (list xmax ymin zmin)
          (list xmax ymax zmin)
          (list xmin ymin zmax)
          (list xmin ymax zmax)
          (list xmax ymin zmax)
          (list xmax ymax zmax)))
      (setq bounds nil)
      (foreach point corners
        (setq bounds
          (aar:add-bound-point bounds
            (aar:insert-local-to-world
              entity data block-base point))))
      bounds)
    nil))

(defun aar:outside-distance
  (point minimum maximum / dx dy dz)
  (setq dx
    (cond
      ((< (car point) (car minimum)) (- (car minimum) (car point)))
      ((> (car point) (car maximum)) (- (car point) (car maximum)))
      (T 0.0)))
  (setq dy
    (cond
      ((< (cadr point) (cadr minimum)) (- (cadr minimum) (cadr point)))
      ((> (cadr point) (cadr maximum)) (- (cadr point) (cadr maximum)))
      (T 0.0)))
  (setq dz
    (cond
      ((< (caddr point) (caddr minimum)) (- (caddr minimum) (caddr point)))
      ((> (caddr point) (caddr maximum)) (- (caddr point) (caddr maximum)))
      (T 0.0)))
  (sqrt (+ (* dx dx) (* dy dy) (* dz dz))))

(defun aar:resolve-anchor
  (entity type insertion box / minimum maximum span tolerance distance center)
  (if (and (= type "INSERT") insertion box)
    (progn
      (setq minimum (car box))
      (setq maximum (cadr box))
      (setq span
        (max
          (- (car maximum) (car minimum))
          (- (cadr maximum) (cadr minimum))
          (- (caddr maximum) (caddr minimum))))
      (setq tolerance (max 0.000001 (* span 0.10)))
      (setq distance
        (aar:outside-distance insertion minimum maximum))
      (if (> distance tolerance)
        (progn
          (setq center
            (list
              (/ (+ (car minimum) (car maximum)) 2.0)
              (/ (+ (cadr minimum) (cadr maximum)) 2.0)
              (/ (+ (caddr minimum) (caddr maximum)) 2.0)))
          (list center "BOUNDS_CENTER_WCS"))
        (list insertion "INSERTION_WCS")))
    (list insertion "ENTITY_POINT_WCS")))

(defun aar:block-name (entity data / object result)
  (setq result (cdr (assoc 2 data)))
  (setq object
    (vl-catch-all-apply 'vlax-ename->vla-object (list entity)))
  (if (and object (not (vl-catch-all-error-p object)))
    (progn
      (setq object
        (vl-catch-all-apply
          'vlax-get-property
          (list object 'EffectiveName)))
      (if (and object (not (vl-catch-all-error-p object)))
        (setq result object))))
  (if result result ""))

(defun aar:text-value (data type / result item)
  (setq result "")
  (if (= type "MTEXT")
    (foreach item data
      (if (or (= (car item) 3) (= (car item) 1))
        (setq result (strcat result (cdr item)))))
    (if (and
          (member type '("TEXT" "ATTRIB"))
          (cdr (assoc 1 data)))
      (setq result (cdr (assoc 1 data)))))
  result)

(setq *aar:block-profile-cache* nil)

(defun aar:block-profile
  (name / cached block entity data type bounds minimum maximum
   width height aspect lines polylines circles arcs curves solids
   texts nested others primitive signature profile)
  (setq cached (assoc name *aar:block-profile-cache*))
  (if cached
    (cdr cached)
    (progn
      (setq lines 0)
      (setq polylines 0)
      (setq circles 0)
      (setq arcs 0)
      (setq curves 0)
      (setq solids 0)
      (setq texts 0)
      (setq nested 0)
      (setq others 0)
      (setq primitive 0)
      (setq bounds nil)
      (setq block (if name (tblobjname "BLOCK" name) nil))
      (if block
        (progn
          (setq entity (entnext block))
          (while entity
            (setq data (entget entity))
            (setq type (cdr (assoc 0 data)))
            (if (= type "ENDBLK")
              (setq entity nil)
              (progn
                (setq primitive (1+ primitive))
                (setq bounds (aar:add-entity-points bounds data))
                (cond
                  ((= type "LINE") (setq lines (1+ lines)))
                  ((member type '("LWPOLYLINE" "POLYLINE"))
                    (setq polylines (1+ polylines)))
                  ((= type "CIRCLE") (setq circles (1+ circles)))
                  ((= type "ARC") (setq arcs (1+ arcs)))
                  ((member type '("ELLIPSE" "SPLINE"))
                    (setq curves (1+ curves)))
                  ((member type '("SOLID" "TRACE" "3DFACE" "HATCH"))
                    (setq solids (1+ solids)))
                  ((member type '("TEXT" "MTEXT" "ATTRIB" "ATTDEF"))
                    (setq texts (1+ texts)))
                  ((= type "INSERT") (setq nested (1+ nested)))
                  (T (setq others (1+ others))))
                (setq entity (entnext entity)))))))
      (setq minimum (if bounds (car bounds) '(0.0 0.0 0.0)))
      (setq maximum (if bounds (cadr bounds) '(0.0 0.0 0.0)))
      (setq width (abs (- (car maximum) (car minimum))))
      (setq height (abs (- (cadr maximum) (cadr minimum))))
      (setq aspect
        (if (> (max width height) 0.000001)
          (/ (min width height) (max width height))
          0.0))
      ;; Assinatura estrutural independente do nome e da escala de insercao.
      ;; O nome bruto continua em campo separado apenas para auditoria.
      (setq signature
        (strcat
          "L" (itoa lines)
          "P" (itoa polylines)
          "C" (itoa circles)
          "A" (itoa arcs)
          "V" (itoa curves)
          "S" (itoa solids)
          "T" (itoa texts)
          "I" (itoa nested)
          "O" (itoa others)
          "R" (rtos aspect 2 3)))
      (setq profile (list signature primitive nested))
      (setq *aar:block-profile-cache*
        (cons (cons name profile) *aar:block-profile-cache*))
      profile)))

(defun aar:write-entity
  (file entity depth root-handle stable-path /
   data type handle layer raw-point insertion point
   rotation sx sy sz block text box anchor-result anchor-source
   minimum maximum has-box raw-block profile signature
   primitive nested)
  (setq data (entget entity))
  (setq type (cdr (assoc 0 data)))
  (setq handle (cdr (assoc 5 data)))
  (setq layer (cdr (assoc 8 data)))
  (setq raw-point (cdr (assoc 10 data)))
  (setq insertion (aar:world-point entity raw-point))
  (setq box
    (if (= type "INSERT")
      (aar:insert-geometry-box entity data)
      (progn
        (setq box (aar:bounding-box entity))
        (if box box (aar:fallback-box entity data)))))
  (setq anchor-result
    (aar:resolve-anchor entity type insertion box))
  (setq point (car anchor-result))
  (setq anchor-source (cadr anchor-result))
  (setq has-box (if box "1" "0"))
  (setq minimum (if box (car box) '(0.0 0.0 0.0)))
  (setq maximum (if box (cadr box) '(0.0 0.0 0.0)))
  (setq rotation (cdr (assoc 50 data)))
  (setq sx (cdr (assoc 41 data)))
  (setq sy (cdr (assoc 42 data)))
  (setq sz (cdr (assoc 43 data)))
  (setq raw-block
    (if (= type "INSERT")
      (if (cdr (assoc 2 data)) (cdr (assoc 2 data)) "")
      ""))
  (setq block (if (= type "INSERT") (aar:block-name entity data) ""))
  (setq profile
    (if (= type "INSERT")
      (aar:block-profile raw-block)
      (list "" 0 0)))
  (setq signature (car profile))
  (setq primitive (cadr profile))
  (setq nested (caddr profile))
  (setq text (aar:text-value data type))
  (write-line
    (strcat
      "ENTITY|"
      (aar:encode handle) "|"
      (aar:encode type) "|"
      (aar:encode layer) "|"
      (aar:encode block) "|"
      (aar:number (if point (car point) 0.0)) "|"
      (aar:number (if point (cadr point) 0.0)) "|"
      (aar:number (if point (caddr point) 0.0)) "|"
      (aar:number
        (aar:degrees
          (aar:world-rotation entity (if rotation rotation 0.0)))) "|"
      (aar:number (if sx sx 1.0)) "|"
      (aar:number (if sy sy 1.0)) "|"
      (aar:number (if sz sz 1.0)) "|"
      (aar:encode text) "|"
      "WCS|"
      (aar:encode anchor-source) "|"
      (aar:number (car insertion)) "|"
      (aar:number (cadr insertion)) "|"
      (aar:number (caddr insertion)) "|"
      has-box "|"
      (aar:number (car minimum)) "|"
      (aar:number (cadr minimum)) "|"
      (aar:number (caddr minimum)) "|"
      (aar:number (car maximum)) "|"
      (aar:number (cadr maximum)) "|"
      (aar:number (caddr maximum)) "|"
      (itoa depth) "|"
      (aar:encode root-handle) "|"
      (aar:encode stable-path) "|"
      (aar:encode raw-block) "|"
      (aar:encode signature) "|"
      (itoa primitive) "|"
      (itoa nested))
    file)

  (setq *aar:record-count* (1+ *aar:record-count*))
  (if (= type "INSERT")
    (setq *aar:insert-record-count* (1+ *aar:insert-record-count*)))
  (if (> depth 0)
    (setq *aar:expanded-record-count*
      (1+ *aar:expanded-record-count*))))

(defun aar:entities-after
  (marker final / result current done)
  (setq result nil)
  (setq done nil)
  (setq current (entnext marker))
  (while (and current (not done))
    (setq result (cons current result))
    (if (= current final)
      (setq done T)
      (setq current (entnext current))))
  (reverse result))

(defun aar:explode-native (entity / marker final)
  (setq marker (entlast))
  (command "_.EXPLODE" entity "")
  (setq final (entlast))
  (if (or (null marker) (= marker final))
    nil
    (aar:entities-after marker final)))

(defun aar:expand-insert
  (file entity depth root-handle stable-path /
   children index child-entity child-data
   child-type child-block child-path)
  (if (<= depth 8)
    (progn
      (setq children (aar:explode-native entity))
      (if children
        (progn
          (setq index 0)
          (foreach child-entity children
            (setq child-data (entget child-entity))
            (if child-data
              (progn
                (setq child-type (cdr (assoc 0 child-data)))
                (setq child-block
                  (if (= child-type "INSERT")
                    (if (cdr (assoc 2 child-data))
                      (cdr (assoc 2 child-data))
                      "")
                    child-type))
                (setq child-path
                  (strcat
                    stable-path "/"
                    (itoa index) ":"
                    child-block))
                (aar:write-entity
                  file child-entity depth root-handle child-path)
                (if (= child-type "INSERT")
                  (aar:expand-insert
                    file
                    child-entity
                    (1+ depth)
                    root-handle
                    child-path))))
            (setq index (1+ index))))))))

(defun aar:export
  (output-path / file selection index entity data type handle
   source-count root-entities previous-qaflags)
  ;; Atualiza os extents sem gravar o desenho. Isso torna GetBoundingBox
  ;; confiavel no Core Console antes de calcular ancoras de exibicao.
  (command "_.REGEN")
  (setq *aar:block-bounds-cache* nil)
  (setq *aar:block-profile-cache* nil)
  (setq *aar:record-count* 0)
  (setq *aar:insert-record-count* 0)
  (setq *aar:expanded-record-count* 0)
  (setq file (open output-path "w" "utf8"))
  (if (null file)
    (progn
      (prompt "\nAAR_ERROR: nao foi possivel criar o inventario.")
      nil)
    (progn
      (write-line "AAR|3" file)
      (setq selection (ssget "_X" '((410 . "Model"))))
      (setq source-count (if selection (sslength selection) 0))
      (setq root-entities nil)
      (if selection
        (progn
          (setq index 0)
          (repeat (sslength selection)
            (setq entity (ssname selection index))
            (setq data (entget entity))
            (setq handle (cdr (assoc 5 data)))
            (aar:write-entity file entity 0 handle handle)
            (if (= (cdr (assoc 0 data)) "INSERT")
              (setq root-entities (cons entity root-entities)))
            (setq index (1+ index)))))
      ;; Abre cada bloco em memoria sobre a copia tecnica. As entidades
      ;; resultantes recebem caminho hierarquico ate o handle original e o
      ;; desenho nunca e salvo.
      (if root-entities
        (progn
          (setq previous-qaflags (getvar "QAFLAGS"))
          (setvar "QAFLAGS" 1)
          (command "_.-LAYER" "_Unlock" "*" "_Thaw" "*" "_On" "*" "")
          (foreach entity (reverse root-entities)
            (setq data (entget entity))
            (if data
              (progn
                (setq handle (cdr (assoc 5 data)))
                (aar:expand-insert file entity 1 handle handle))))
          (setvar "QAFLAGS" previous-qaflags)))
      (write-line
        (strcat
          "SUMMARY|"
          (itoa *aar:record-count*) "|"
          (itoa *aar:insert-record-count*) "|"
          (itoa source-count) "|"
          (itoa *aar:expanded-record-count*))
        file)
      (close file)
      (prompt (strcat "\nAAR_OK:" output-path))
      T)))

(princ "\nAutoAIBuilder Recognition Inventory 3.0 carregado.")
(princ)
