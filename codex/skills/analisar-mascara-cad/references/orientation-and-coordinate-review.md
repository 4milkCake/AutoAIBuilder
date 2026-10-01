# Revisão de coordenadas e orientação

## Papéis diferentes

- `LEGEND_EXEMPLAR`: símbolo usado apenas para explicar a legenda.
- `PLANT_OCCURRENCE`: ocorrência real que representa um ponto da planta.

O mesmo nome de bloco pode existir nos dois papéis com posição, escala e
rotação diferentes. Usar a legenda para aprender a semântica; usar somente a
ocorrência real para gerar coordenada de lançamento.

## Sistemas de coordenadas

O inventário AAR v3 registra WCS. O painel Propriedades do AutoCAD pode exibir
o UCS atual. Antes de declarar discrepância:

1. comparar nome/assinatura do bloco, rotação e escala de ocorrências reais;
2. obter pelo menos duas correspondências;
3. estimar a transformação UCS→WCS;
4. conferir resíduos nas demais correspondências;
5. invalidar a relação quando o UCS, o arquivo ou o pavimento mudar.

Caso real `PONTOS TÉRREO.dwg`, observado em 31/07/2026:

```text
Xwcs ≈ Xpropriedades + 662,9970
Ywcs ≈ Ypropriedades + 1534,7829
```

Quatro ocorrências distribuídas reproduziram a translação dentro da precisão
de quatro casas mostrada pelo painel. Essa relação pertence somente ao estado
de UCS desse arquivo e não é uma calibração do Builder.

## Escala

Registrar separadamente:

```text
effective_scale = property_scale * block_unit_factor
```

No caso observado, o painel mostrou `Unit factor = 0,1`. Assim, escala
`34,5179` nas Propriedades corresponde a aproximadamente `3,45179` no AAR.
Não interpretar essa diferença como escala geométrica do projeto sem conferir
o fator de unidade e transformações de blocos pais.

## Orientação visual efetiva

Manter campos distintos:

- `raw_rotation_deg`: rotação numérica da referência de bloco;
- `native_semantic_axis_deg`: direção relevante na definição do símbolo;
- `effective_orientation_deg`: eixo semântico após a transformação completa;
- `effective_direction`: `RIGHT`, `UP`, `LEFT`, `DOWN`, `OBLIQUE`,
  `NOT_APPLICABLE` ou `PENDING_VISUAL_REVIEW`;
- `wall_axis_deg` e `room_side`, quando houver parede associada;
- `orientation_evidence`: geometria, parede, linha de chamada, textos e imagem.

Não usar a tabela `0°=direita`, `90°=cima`, `180°=esquerda`, `270°=baixo`
diretamente sobre `raw_rotation_deg`. Ela só vale se o eixo semântico nativo do
bloco apontar para a direita, não houver espelhamento e o contexto confirmar.

Procedimento:

1. aplicar rotação, escalas com sinal, fator de unidade e transformações pais;
2. identificar na geometria o eixo semântico: ponta, normal, haste ou abertura;
3. localizar a parede ou superfície de montagem mais plausível;
4. verificar perpendicularidade e qual lado aponta para o ambiente;
5. comparar com textos e símbolos vizinhos;
6. registrar confiança e manter pendente diante de conflito.

Exemplos visuais confirmados pelo usuário no caso real em 31/07/2026:

- quadro, bloco `DSLA~ÇDLÃÇSLD~ÇALDÃLD`: rotação bruta `270°`, direção efetiva
  `DOWN`; a parede está acima e o quadro se volta para o ambiente abaixo;
- tomada do ponto de gás, bloco `asdasdasdad`: rotação bruta `90°`, direção
  efetiva `LEFT`; a alvenaria da ilha está à direita do ponto;
- interfone, bloco `asçlkdçalkdsçlxzcnzcnzsaldkal`: rotação bruta `0°`, mas
  triângulo efetivo apontando para a esquerda, perpendicular à parede;
- tomada de televisão, bloco `65sd465ad4s65asd4`: rotação bruta `90°`, mas
  triângulo efetivo apontando para baixo.

Esses exemplos demonstram a regra; não criam um ajuste universal para outros
nomes de bloco.

## Modos de orientação luminotécnica

Escolher o modo antes de interpretar ângulo:

- `NOT_APPLICABLE`: luminária de teto centralizada e sem distribuição
  direcional explícita. Não exigir direção para lançamento.
- `FACE_DIRECTION`: arandela ou luminária fixada em parede. Registrar parede,
  normal, lado do ambiente e face efetiva de iluminação.
- `PATH_GEOMETRY`: fita ou perfil de LED. Preservar o percurso pai, subdividir
  cada mudança de direção em uma unidade reta e gerar uma âncora no ponto médio
  de cada segmento (`SEGMENT_MIDPOINT_TANGENT`). Um L produz duas peças; um
  retângulo fechado produz quatro.
- `PENDING_VISUAL_REVIEW`: spot direcionável, luminária assimétrica ou caso em
  que tipo e geometria não permitem escolher com segurança.

Não confundir o eixo geométrico de uma luminária linear de teto com direção de
iluminação. Um eixo pode ser necessário para posicionamento sem representar
uma face iluminante.

### Âncora de lançamento para fita e perfil de LED

1. separar cada percurso contínuo dos demais e atribuir um `parent_path_id`;
2. dividir nos vértices onde a direção muda; unir somente segmentos consecutivos
   comprovadamente colineares;
3. para cada segmento reto, usar a média dos extremos como âncora e o vetor
   início→fim como rotação geométrica;
4. registrar `segment_index`, `segment_count`, extremos e handles geométricos;
5. se o inventário fornecer apenas limites, não usar o centro global de um L ou
   retângulo. Reconstruir os lados somente quando a forma e a confirmação visual
   forem inequívocas; caso contrário, manter a segmentação pendente;
6. em curva, ramificação, sobreposição ou geometria sem vértices confiáveis,
   manter revisão visual antes de criar as unidades de lançamento.

A rotação geométrica acima acompanha o sentido armazenado pelo desenho. Ela não
é evidência do ponto de alimentação elétrica. Manter `flow_direction` e
`feed_point_wcs` pendentes sem indicação explícita.

## Regressão de arandelas

No luminotécnico do segundo pavimento revisado em 02/08/2026, duas arandelas
V2 foram inicialmente inferidas como `UP` e corrigidas profissionalmente para
`DOWN` (`270°` efetivos). Usar o caso para comprovar que:

- tipo e quantidade aprovados não resolvem a direção;
- a direção deve ser registrada por ocorrência;
- a confirmação visual do usuário prevalece sobre uma inferência contextual;
- `DOWN` desse arquivo não cria regra universal para símbolos V2 ou para outra
  varanda.
