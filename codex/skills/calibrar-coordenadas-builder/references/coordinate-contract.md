# Contrato de coordenadas

## Espaços

1. `CAD_INSERTION` — inserção original auditável.
2. `CAD_ANCHOR_WCS` — âncora visual aprovada no DWG.
3. `BUILDER_MODEL` — coordenada no modelo do Builder.
4. `SCREEN_PIXEL` — projeção temporária da interface.

Não persistir `SCREEN_PIXEL` como coordenada de engenharia.

## Pontos de controle

Campos:

| Campo | Regra |
|---|---|
| `point_id` | identificador estável |
| `source_x/source_y` | âncora WCS |
| `target_x/target_y` | coordenada de modelo Builder |
| `role` | `FIT` ou `VALIDATION` |

Usar no mínimo dois pontos `FIT` distintos para a transformação de
similaridade. Preferir três ou mais pontos distribuídos e reservar pelo menos
um ponto `VALIDATION`.

## Saída

O contrato `autoaibuilder-coordinate-calibration/1.0` deve conter:

- modelo matemático;
- unidades;
- coeficientes `a`, `b`, `tx`, `ty`;
- escala e rotação;
- pontos usados;
- coordenadas previstas;
- resíduos X/Y e erro euclidiano;
- RMS e erro máximo por grupo;
- estado `CALCULATED_AWAITING_HUMAN_APPROVAL`;
- identidade do DWG, projeto e pavimento adicionada pelo agente;
- aprovação humana adicionada como evento separado.

## Invalidação

Invalidar quando mudar:

- SHA-256 do DWG;
- projeto ou pavimento;
- unidade;
- origem ou escala da referência;
- peça ou regra de offset local.

