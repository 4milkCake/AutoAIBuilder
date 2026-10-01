# Casos de regressão — Pontos Térreo

Usar estes casos somente para o DWG com SHA-256
`6F40D98A8EB17F4545028198CE1F1BCEB4BF6BD14DA1FBD2C48692B2B44F36E2`.
Outro hash exige novo inventário e nova validação visual.

## Sistema de coordenadas

- O inventário AAR está em WCS.
- A divergência observada no painel Propriedades decorreu do UCS ativo, não de
  escala global. A translação observada foi aproximadamente `+662,997` em X e
  `+1534,783` em Y, válida somente para o UCS verificado nessa sessão.

## Decisões semânticas confirmadas

- Área externa, piscina e ducha: três ralos, sendo um de esgoto e dois
  pluviais. Preservar a subcategoria do sistema mesmo quando compartilham a
  geometria de ralo maior.
- As duas tomadas altas da área externa são genéricas. Não associá-las a um
  equipamento específico.
- Coifa de bancada: normalizar como tomada comum para o projeto e preservar
  `coifa de bancada` como nota de aplicação.
- Altura do ponto hidráulico para filtro e altura da torneira de teto não são
  informadas pela legenda; não completar por inferência.

## Falsos negativos por geometria solta

Três ocorrências verdes de tomadas a 1,10 m não são referências de bloco de
primeiro nível. Elas são compostas por linhas no layer `pontos` e precisam ser
reconstruídas visualmente:

- lavabo: uma tomada simples, handles `37B1B` a `37B20`;
- área de serviço: uma tomada dupla, handles `37AEA` a `37AFF`, agrupada como
  uma única ocorrência física;
- área de serviço: uma tomada simples, handles `37B01` a `37B06`.

Preservar a lista completa de handles e a origem da âncora. Este caso prova que
contar apenas `INSERT` produz falso negativo mesmo quando a legenda e a planta
estão visualmente coerentes.

## Estado depois do lote 1

- 94 ocorrências registradas: 64 elétricas, 24 hidráulicas, 4 sanitárias e 2
  pluviais;
- 46 decisões semânticas aprovadas, 8 corrigidas e 40 ainda pendentes;
- orientação em relação às paredes continua separada da aprovação semântica;
- estado global permanece `PENDING_REVIEW`.

## Varredura completa de tomadas verdes

A assinatura formada por três lados de aproximadamente 34–50 unidades WCS,
proporção máxima/mínima de 1,12 e uma haste de 5–16 unidades ligada ao ponto
médio de um lado recuperou todos os símbolos verdes soltos deste hash:

- 10 triângulos geométricos;
- 9 ocorrências físicas, porque dois triângulos formam uma tomada dupla;
- 8 tomadas simples e 1 tomada dupla a 1,10 m;
- 3 ocorrências já confirmadas pelo usuário;
- 6 novos candidatos: 1 na casa de máquinas da piscina, 3 na
  cozinha/área gourmet e 2 no banheiro térreo.

Uma das ocorrências do banheiro cai na faixa geométrica antes usada para a
sala, mas pertence ao banheiro pelo fechamento arquitetônico. O texto local
identifica outra como tomada para toalheiro aquecido; preservar a aplicação
sem criar um tipo elétrico especial.

Estado depois da confirmação da varredura: 100 ocorrências registradas — 70
elétricas, 24 hidráulicas, 4 sanitárias e 2 pluviais. As 9 tomadas verdes estão
semanticamente aprovadas; o pacote completo permanece `PENDING_REVIEW` por
causa dos ambientes ainda não revisados.

## Fechamento da revisão semântica

Na revisão final por ambiente, o usuário corrigiu duas fronteiras visuais que
não podem ser resolvidas apenas por faixas X/Y:

- os handles `376ED` e `37780`, inicialmente atribuídos ao banheiro térreo,
  pertencem à sala e já estavam abrangidos pela aprovação visual desse
  ambiente;
- a tomada dupla grifada no perímetro, handle `37505`, é o ponto do hall de
  entrada; o candidato `37528` foi rejeitado para não criar uma segunda
  ocorrência física na revisão;
- os blocos hidráulicos `30D30` e `35FC3` têm coordenadas e geometria idênticas
  e foram consolidados como uma única torneira física, preservando ambos os
  handles.

Estado semântico aprovado: 99 linhas auditáveis, sendo 86 aprovadas, 12
corrigidas e 1 rejeitada. Isso corresponde a 98 ocorrências físicas: 69
elétricas, 23 hidráulicas, 4 sanitárias e 2 pluviais. A orientação em relação
às paredes permanece uma etapa separada; por isso o estado global continua
`PENDING_REVIEW` até a revisão direcional.
