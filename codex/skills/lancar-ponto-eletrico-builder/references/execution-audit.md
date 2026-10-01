# Auditoria de execução

Registrar um objeto JSON:

```json
{
  "contract_version": "autoaibuilder-builder-placement/1.0",
  "run_id": "uuid",
  "status": "PLANNED",
  "project": "",
  "floor": "",
  "discipline": "FIACAO",
  "point": {
    "candidate_id": "",
    "handle": "",
    "semantic_code": "",
    "description": "",
    "position_wcs": {"x": 0.0, "y": 0.0, "z": 0.0}
  },
  "calibration": {
    "contract": "autoaibuilder-coordinate-calibration/1.0",
    "approved": false
  },
  "piece": {
    "catalog_text": "",
    "amperage_a": null,
    "position": "",
    "orientation": "",
    "offset_status": "PENDING"
  },
  "actions": [],
  "evidence_before": [],
  "evidence_after": [],
  "user_approved_result": false,
  "undo_tested": false,
  "created_at": "ISO-8601"
}
```

Estados:

- `PLANNED`
- `BLOCKED`
- `AWAITING_CONFIRMATION`
- `EXECUTED_AWAITING_REVIEW`
- `APPROVED_BY_USER`
- `REJECTED_BY_USER`
- `UNDONE`
- `FAILED_SAFE`

Não usar `APPROVED_BY_USER` sem aprovação explícita.

