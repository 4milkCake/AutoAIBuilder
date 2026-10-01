# Contratos de saída

## `review-decisions.jsonl`

Gravar uma linha JSON por decisão:

```json
{
  "session_id": "uuid",
  "candidate_id": "uuid",
  "handle": "18E8B",
  "decision": "APPROVED",
  "semantic_code": "ELE_SAIDA_SOM",
  "description": "SAÍDA DE SOM",
  "entity_role": "PLANT_OCCURRENCE",
  "anchor_source": "EXPANDED_GEOMETRY_CENTER_WCS",
  "position_wcs": {"x": 0.0, "y": 0.0, "z": 0.0},
  "coordinate_comparison": {
    "reported_system": "UCS_CURRENT",
    "reported_position": {"x": 0.0, "y": 0.0, "z": 0.0},
    "ucs_to_wcs_status": "VALIDATED_FOR_CURRENT_UCS"
  },
  "scale": {
    "property_scale_x": 1.0,
    "block_unit_factor": 1.0,
    "effective_scale_x": 1.0
  },
  "orientation": {
    "orientation_mode": "FACE_DIRECTION",
    "raw_rotation_deg": 0.0,
    "effective_direction": "PENDING_VISUAL_REVIEW",
    "effective_orientation_deg": null,
    "wall_axis_deg": null,
    "evidence": []
  },
  "note": "",
  "reviewed_at": "ISO-8601"
}
```

Para fita ou perfil de LED, gravar uma decisão por segmento reto de lançamento
e preservar o vínculo com o percurso pai:

```json
{
  "parent_path_id": "perfil-sala-01",
  "segment_id": "perfil-sala-01-seg-01",
  "segment_index": 1,
  "segment_count": 2,
  "orientation": {
    "orientation_mode": "PATH_GEOMETRY",
    "parent_path_closed": false,
    "segment_wcs": [
      {"x": 0.0, "y": 0.0, "z": 0.0},
      {"x": 100.0, "y": 0.0, "z": 0.0}
    ],
    "placement_strategy": "SEGMENT_MIDPOINT_TANGENT",
    "placement_anchor_wcs": {"x": 0.0, "y": 0.0, "z": 0.0},
    "placement_anchor_source": "LINE_MIDPOINT",
    "placement_anchor_status": "EXACT",
    "path_tangent_deg": 0.0,
    "rotation_status": "RESOLVED_FROM_PATH",
    "flow_direction": "PENDING",
    "feed_point_wcs": null
  }
}
```

Valores de `placement_anchor_source`:

- `LINE_MIDPOINT`: média exata dos extremos de uma linha;
- `RECONSTRUCTED_SEGMENT_MIDPOINT`: média dos extremos de um lado reconstruído
  de forma inequívoca e confirmado visualmente;
- `BOUNDS_CENTER_FALLBACK`: centro aproximado dos limites quando faltarem
  vértices ordenados.

Não usar o centro da caixa envolvente global como peça única para um L,
retângulo ou percurso com mudança de direção. Se os segmentos não puderem ser
reconstruídos com segurança, usar `placement_anchor_status=PENDING_SEGMENTATION`
e `rotation_status=PENDING_MANUAL`. Não preencher `flow_direction` ou
`feed_point_wcs` sem evidência explícita.

Valores válidos de `decision`:

- `APPROVED`
- `REJECTED`
- `CORRECTED`
- `PENDING`

## `legend-review-decisions.jsonl`

Gravar decisões sobre itens da legenda separadamente das ocorrências da
planta:

```json
{
  "session_id": "uuid-ou-id-da-execucao",
  "description_handle": "37970",
  "symbol_handle": null,
  "decision": "CORRECTED",
  "original_description": "tomada para coifa de bancada",
  "normalized_type": "TOMADA_NORMAL",
  "application_note": "coifa de bancada",
  "coordinate_role": "LEGEND_EXEMPLAR",
  "note": "Tipo normalizado confirmado pelo usuário; aplicação preservada.",
  "reviewed_at": "ISO-8601"
}
```

Não copiar posição, escala ou rotação do exemplar da legenda para uma
ocorrência da planta.

## `orientation-review-decisions.jsonl`

Gravar uma linha por orientação efetiva confirmada em uma ocorrência real:

```json
{
  "session_id": "uuid-ou-id-da-execucao",
  "review_scope": "ORIENTATION_ONLY",
  "plant_handle": "37B29",
  "block_name": "nome-do-bloco",
  "semantic_description": "ponto para interfone",
  "decision": "APPROVED",
  "position_wcs": {"x": 0.0, "y": 0.0, "z": 0.0},
  "raw_rotation_deg": 0.0,
  "effective_direction": "LEFT",
  "effective_orientation_deg": 180.0,
  "architectural_context": "parede à direita; ponto voltado ao ambiente",
  "evidence": ["USER_VISUAL_CONFIRMATION", "BLOCK_GEOMETRY", "WALL_CONTEXT"],
  "reviewed_at": "ISO-8601"
}
```

Usar `review_scope=ORIENTATION_ONLY` para não transformar a confirmação da
direção em aprovação automática de todos os demais campos semânticos.

## `mask-recognition-manifest.json`

Incluir:

- versão do contrato;
- caminho informativo e SHA-256 da origem;
- ID da sessão;
- unidade e sistema de coordenadas;
- UCS observado e transformação UCS→WCS, quando houver comparação com o painel
  Propriedades;
- totais do inventário;
- totais por origem da âncora;
- legenda detectada, pares e descrições sem par;
- aprovados, rejeitados, corrigidos e pendentes;
- falsos positivos e ausências conhecidos;
- totais separados de exemplares da legenda e ocorrências da planta;
- orientações efetivas aprovadas, pendentes e não aplicáveis;
- integridade do original;
- estado final `PENDING_REVIEW` ou `APPROVED_BY_USER`.

Não emitir `APPROVED_BY_USER` sem manifestação explícita do usuário.

## Pacote de revisão por ambiente

Quando a revisão visual for feita por ambiente, produzir também:

- `environment-summary.json`: totais por ambiente, código semântico e comando;
- `environment-coordinate-register.csv`: uma linha por ocorrência física ou
  segmento, com ID estável, ambiente, WCS, decisão e orientação;
- `profile-anchor-register.json`: uma entrada por segmento reto, preservando
  percurso pai, extremos, ponto médio, tangente e sentido elétrico separado;
- `special-led-point-register.json`: posições de energia para LED sem caixa,
  sem inventar peça do Builder;
- `orientation-review-decisions.jsonl`: confirmações ou correções de direção
  que não devem aprovar implicitamente outros campos;
- capturas gerais e recortes por ambiente listados em `visual_evidence` no
  manifesto.

IDs no CSV devem ser não vazios e únicos. Toda linha deve conter X/Y WCS e uma
decisão válida. Entidades coincidentes agrupadas em uma posição devem preservar
todos os handles em vez de apagar a duplicidade de origem.

## `loose-outlet-candidates.json`

Gerar com `scripts/scan_loose_outlet_triangles.py` quando símbolos de tomada
podem estar desenhados como linhas soltas. Cada candidato deve conter:

- ID estável e todos os handles associados;
- quantidade de triângulos e sugestão simples/dupla/tripla;
- âncora WCS conservadora e sua origem;
- eixo geométrico bruto;
- estado da cor, que normalmente não está disponível no AAR;
- decisão `PENDING` e revisões obrigatórias de cor, legenda, ambiente e
  orientação.

Não copiar automaticamente o candidato para o registro aprovado. Dois ou três
triângulos alinhados podem representar uma única tomada dupla ou tripla, e não
duas ou três posições físicas.
