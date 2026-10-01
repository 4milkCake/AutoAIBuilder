# Regras do domínio

## Fonte de verdade

- Priorizar a legenda do próprio projeto.
- Combinar layer, bloco, geometria, cor, texto próximo, legenda e contexto.
- Não classificar apenas pelo nome do bloco; nomes podem ser aleatórios.
- Preservar textos como `20A`, `220V`, alturas e observações.
- Preservar a aplicação original, como `coifa`, separada do tipo normalizado.
  Equipamento específico não implica automaticamente uma peça elétrica
  especial; exigir evidência ou confirmação profissional.
- Manter os dois símbolos e os textos de uma tomada dupla.
- Encaminhar ambiguidade e baixa confiança para revisão humana.

## Geometria e coordenadas

- Trabalhar em WCS.
- Rotular cada entidade como `LEGEND_EXEMPLAR` ou `PLANT_OCCURRENCE`.
- Usar o exemplar da legenda para semântica, nunca como coordenada do ponto.
- Tratar coordenadas do painel Propriedades como possível UCS atual e validar
  a transformação para WCS com correspondências reais.
- Preservar o ponto de inserção original.
- Usar `INSERTION_WCS` quando o ponto-base pertence à geometria.
- Usar `BOUNDS_CENTER_WCS` quando o ponto-base está fora dos limites do bloco.
- Usar `EXPANDED_GEOMETRY_CENTER_WCS` quando a hierarquia expandida demonstra
  que a inserção raiz está longe do símbolo visível.
- Preservar raiz, profundidade, caminho estável e assinatura geométrica.
- Não reduzir todo ângulo a quatro direções; manter o ângulo bruto.
- Separar rotação bruta, eixo nativo e orientação visual efetiva.
- Inferir direção pela geometria transformada e pelo contexto da parede, não
  apenas pelo campo `Rotation`.
- Considerar fator de unidade, escalas negativas, espelhamento e blocos pais.

## Legenda e hierarquia

- Exigir cabeçalho explícito `LEGENDA` ou `SIMBOLOGIA`.
- Impedir que uma descrição ou símbolo seja usado em mais de um par.
- Rejeitar contradições semânticas explícitas.
- Excluir da fila os exemplares usados dentro da legenda.
- Manter descrições sem par e símbolos sem classificação como pendências.
- Não concluir ausência apenas porque o símbolo não aparece como `INSERT`.
  Depois do inventário de blocos, revisar também conjuntos de `LINE`,
  `LWPOLYLINE`, `HATCH`, círculo e texto que reproduzam a geometria e a cor da
  legenda. Agrupar linhas soltas somente quando assinatura, proximidade,
  continuidade e revisão visual sustentarem uma única ocorrência física.
- Para ocorrência reconstruída de geometria solta, preservar todos os handles,
  usar `RECONSTRUCTED_GEOMETRY_CENTER_WCS` quando o centro for exato ou
  `BOUNDS_CENTER_FALLBACK` quando aproximado, e registrar a decisão humana que
  autorizou o agrupamento. Cor isolada nunca é evidência suficiente.

## Luminotécnico

- Classificar separadamente tipo da luminária, forma de instalação e exigência
  de orientação.
- Tratar textos curtos próximos (`F1`, `S1`, `V1`, `H2`, `Q3`) como códigos de
  comando quando indicarem acionamento. Não usá-los como tipo da luminária; o
  mesmo símbolo mantém o mesmo tipo mesmo com outro comando.
- Não interpretar ângulo de facho (`12°`, `25°`) como rotação ou direção de
  montagem.
- Não deixar palavras da descrição da legenda, como `fachada`, sobreporem o
  contexto real de instalação da ocorrência. Preservar a descrição original e
  normalizar o tipo pela geometria e pela planta.
- Usar `orientation_mode=NOT_APPLICABLE` para luminária de teto centralizada
  cuja distribuição não tenha face ou eixo funcional indicado. Preservar a
  rotação bruta apenas como dado CAD.
- Usar `orientation_mode=FACE_DIRECTION` para arandela e outra luminária de
  parede. Determinar a face iluminante pela geometria transformada, normal da
  parede e lado do ambiente; não usar somente `Rotation`.
- Usar `orientation_mode=PATH_GEOMETRY` para fita e perfil de LED. Preservar o
  encaminhamento completo e seu `parent_path_id`, mas subdividir qualquer
  percurso com mudança de direção em unidades retas independentes para o
  futuro lançamento. Um L gera duas unidades; um retângulo fechado gera quatro.
- Dividir nos vértices onde a direção muda. Unir somente trechos consecutivos
  comprovadamente colineares. Registrar `segment_index`, `segment_count`,
  extremos WCS e vínculo com o percurso original.
- Usar `placement_strategy=SEGMENT_MIDPOINT_TANGENT` em cada reta: calcular a
  média exata dos extremos e usar a direção da reta como rotação geométrica da
  peça. Não gerar uma única âncora para um L, retângulo ou outra polilinha com
  direções distintas.
- Não substituir o ponto médio do comprimento pelo centro da caixa envolvente
  quando os vértices ordenados estiverem disponíveis. Em polilinha cuja
  geometria ordenada não esteja disponível, admitir `BOUNDS_CENTER_FALLBACK`
  somente como centro aproximado e registrar `rotation_status=PENDING_MANUAL`.
- Criar uma âncora por segmento reto de lançamento. Não unir segmentos apenas
  porque pertencem ao mesmo perfil gráfico. Percurso fechado, ramificado,
  curvo ou com vértices indisponíveis exige revisão visual antes de definir a
  segmentação.
- Separar a direção geométrica do desenho da direção elétrica de alimentação.
  Não inventar sentido de alimentação da fita. Registrar `FLOW_DIRECTION`
  somente quando seta, alimentador, texto ou confirmação profissional indicar
  início e fim.
- Manter `PENDING_VISUAL_REVIEW` para luminária de teto assimétrica, spot
  direcionável ou símbolo cuja distribuição luminosa não esteja clara.
- Manter ponto de energia para iluminação LED sem caixa separado das
  luminárias e dos perfis. Não inventar caixa, peça ou método de lançamento no
  Builder.
- Agrupar entidades coincidentes como uma posição física somente com evidência
  geométrica e confirmação; preservar todos os handles no registro.

## Ambientes e revisão visual

- Determinar o ambiente pela arquitetura completa, paredes, limites e
  continuidade espacial. Um recorte isolado pode confundir área de serviço,
  jardim, vazio ou pé-direito duplo com um cômodo adjacente.
- Não usar código de comando para nomear ambiente.
- Quando uma linha ou perfil cruza o limite visual de um recorte, consultar a
  planta completa antes de atribuí-lo ao ambiente.
- Depois que a legenda estiver estável, revisar por ambiente para reduzir a
  carga humana; conservar no artefato auditável uma linha por ocorrência ou
  segmento.
- Aprovação de tipo e quantidade não aprova automaticamente ambiente,
  coordenada ou orientação. Registrar correções de cada dimensão separadamente.

## Preservação

- Não apagar entidades, layers, blocos, referências ou geometria arquitetônica.
- Não executar rotinas históricas de limpeza.
- Não ler nem alterar internamente `CINZA PONTOS` no fluxo histórico.
- Usar cópia técnica e SHA-256 antes/depois.
- Tratar qualquer resultado sem revisão como evidência, não como produção.
