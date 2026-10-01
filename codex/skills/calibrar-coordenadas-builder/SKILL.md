---
name: calibrar-coordenadas-builder
description: Calibrar e validar a transformação entre âncoras WCS de uma máscara DWG e coordenadas de modelo no AltoQi Builder, mantendo separados transformação global, offset local da peça e pixels da interface. Usar para coletar pontos de controle, calcular escala/rotação/translação, medir resíduos, validar orientações e gerar um contrato de calibração auditável antes de qualquer lançamento automatizado.
---

# Calibrar coordenadas do Builder

Calcular uma transformação reproduzível sem executar lançamentos. Manter
qualquer resultado como pendente até aprovação visual do usuário.

## Pré-condições

Exigir:

- manifesto de reconhecimento com pontos WCS aprovados;
- DWG, projeto Builder e pavimento identificados;
- unidades de origem e destino;
- pontos correspondentes coletados no modelo do Builder, não em pixels;
- pasta de saída auditável.

Ler [coordinate-contract.md](references/coordinate-contract.md) antes de
calcular.

## Coletar pontos

Criar CSV UTF-8 com:

```text
point_id,source_x,source_y,target_x,target_y,role
```

Usar `FIT` em pontos distribuídos pela planta e reservar pelo menos um
`VALIDATION`. Evitar pontos quase coincidentes ou todos concentrados em uma
mesma região.

## Calcular

Executar:

```powershell
python scripts/fit_similarity_transform.py <pontos.csv> --output <calibracao.json> --source-unit mm --target-unit cm
```

O modelo inicial é uma transformação de similaridade 2D:

```text
Xb = a * Xw - b * Yw + tx
Yb = b * Xw + a * Yw + ty
```

Ela representa escala uniforme, rotação e translação. Não trocar
silenciosamente para uma transformação afim. Se os resíduos indicarem
deformação, escala diferente por eixo ou outro problema, interromper e
investigar a origem.

## Validar

1. Conferir resíduos de ajuste e validação.
2. Apresentar erro por ponto, RMS e erro máximo.
3. Solicitar ao usuário a tolerância profissional aceitável.
4. Validar visualmente um ponto reservado.
5. Vincular a calibração ao SHA-256 do DWG, projeto e pavimento.
6. Registrar aprovação separadamente; o script nunca autoaprova.
7. Ler [historical-offsets.md](references/historical-offsets.md) antes de
   adicionar offsets locais.

## Separar offset local

Indexar cada offset por:

```text
tipo semântico + peça Builder + altura + orientação
```

Manter `VALIDATED`, `INFERRED` e `PENDING` como estados distintos. Não usar
direita ou cima para automação enquanto continuarem inferidas por simetria.

## Portões

- Nunca usar coordenadas de tela como coordenadas de engenharia.
- Nunca misturar transformação global e offset local.
- Nunca reutilizar calibração após mudança de arquivo, unidade, escala, origem
  ou pavimento.
- Nunca declarar precisão sem resíduos e ponto independente.
- Nunca executar o Builder nesta skill.

