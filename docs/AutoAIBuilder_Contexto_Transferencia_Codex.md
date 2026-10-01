# AutoAIBuilder — Contexto de transferência para o Codex

> Documento de continuidade do projeto  
> Responsável: Danilo Prates Coelho  
> Atualização: 25/07/2026  
> Pasta principal desejada: `D:\Projetos\AutoAIBuilder`

## 1. Instrução principal ao Codex

Use este documento como contexto inicial do projeto **AutoAIBuilder**. Antes de criar, alterar ou executar qualquer código:

1. Inspecione a pasta `D:\Projetos\AutoAIBuilder` e os arquivos existentes.
2. Preserve tudo que já estiver desenvolvido.
3. Não presuma que os nomes e caminhos citados aqui continuam idênticos; confirme no computador.
4. Diferencie claramente:
   - comportamento já validado;
   - hipótese de automação;
   - funcionalidade futura;
   - informação que ainda depende de teste.
5. Não automatize ações destrutivas sem pré-visualização, auditoria e confirmação do usuário.
6. Não altere projetos reais do AutoCAD ou AltoQi Builder durante o desenvolvimento inicial. Use cópias de teste.
7. Trabalhe de maneira incremental, com versões pequenas, testes reproduzíveis, logs e possibilidade de desfazer.

Este documento transfere as decisões e aprendizados da conversa com o ChatGPT. Ele não substitui a inspeção dos códigos AutoLISP, CSVs, DWGs, arquivos do Builder ou demais artefatos que estiverem no computador.

---

## 2. Perfil e objetivo do usuário

Danilo é engenheiro civil e trabalha com projetos complementares de engenharia:

- projeto elétrico;
- projeto hidráulico;
- projeto sanitário;
- projeto estrutural.

Softwares principais:

- AutoCAD 2025 completo;
- AltoQi Builder 2026 Premium;
- TQS.

Objetivo de longo prazo:

Criar uma plataforma desktop local chamada provisoriamente **AutoAIBuilder**, capaz de reunir automações, regras de engenharia, agentes de IA e operação supervisionada de softwares técnicos. A plataforma deverá apoiar desde o preparo da arquitetura até o lançamento, conferência e documentação de projetos complementares.

O projeto poderá futuramente se tornar um produto comercial, mas a prioridade atual é:

1. funcionar no fluxo real do usuário;
2. preservar a responsabilidade técnica e a supervisão humana;
3. manter rastreabilidade;
4. reduzir tarefas repetitivas;
5. validar cada automação antes de ampliá-la.

---

## 3. Decisão de rumo atual

O desenvolvimento das automações dentro do Builder foi temporariamente pausado porque o usuário precisa utilizar o Builder em um projeto real de cliente.

Decisão:

- desenvolver em paralelo a plataforma desktop;
- manter a criação/importação da edificação e da máscara no Builder como etapa manual por enquanto;
- não priorizar agora a automação da comunicação AutoCAD → Builder;
- retomar posteriormente os testes de lançamento no Builder;
- integrar gradualmente à plataforma as rotinas AutoCAD e os dados semânticos já validados.

Essa decisão evita interferir no trabalho produtivo e permite estruturar o produto enquanto as automações ainda estão em evolução.

---

## 4. Visão pretendida do AutoAIBuilder

Aplicação desktop local, visual e orientada por fluxo.

Referência visual já aprovada pelo usuário:

- tema escuro;
- azul como cor principal;
- verde para etapas concluídas;
- amarelo/laranja para alertas e elementos elétricos;
- organização semelhante a um painel técnico;
- navegação lateral;
- fluxo de etapas no topo;
- área central de visualização da planta;
- painel de agentes/tarefas à direita;
- cartões de resumo na parte inferior.

Fluxo conceitual da interface:

1. Importar;
2. Máscara;
3. Identificação;
4. Regras;
5. Proposição;
6. Lançamento;
7. Validação;
8. Relatórios.

Módulos vislumbrados:

- Painel Principal;
- Projetos;
- Arquivos;
- Máscaras;
- Automação;
- Agentes IA;
- Bibliotecas;
- Regras de Projeto;
- Validadores;
- Relatórios;
- Histórico;
- Configurações.

Recurso futuro importante:

Depois da identificação dos elementos, o usuário deseja revisar visualmente cada item em uma galeria/lista, por exemplo:

- miniatura do recorte da tomada;
- posição na planta;
- layer de origem;
- classificação;
- altura;
- corrente nominal;
- tensão;
- orientação;
- confiança;
- situação: aprovado, corrigir ou ignorar.

O objetivo é tornar a conferência mais rápida do que navegar manualmente ponto a ponto no Builder.

---

## 5. Tecnologia planejada

Direção técnica inicial:

- Windows desktop;
- C#;
- .NET 8;
- WPF para a primeira versão;
- Visual Studio Community;
- Git;
- arquitetura modular;
- execução local;
- integração futura com AutoCAD;
- integração supervisionada com AltoQi Builder;
- possível comunicação com a API da OpenAI.

Pasta do código:

`D:\Projetos\AutoAIBuilder`

Pasta sugerida para ferramentas:

`D:\DevTools`

Estrutura inicial sugerida:

```text
D:\Projetos\AutoAIBuilder
├── src
├── tests
├── docs
│   └── design
├── samples
│   ├── autocad
│   └── builder
├── artifacts
└── tools
```

O nome anterior da pasta era `PlataformaEngenharia`, mas o usuário a renomeou para `AutoAIBuilder`.

### Integração com OpenAI

O usuário deseja que o software possa usar IA e, quando aplicável, Computer Use.

Diretriz:

- não presumir que uma assinatura ChatGPT autoriza consumo da API;
- assinatura ChatGPT e API são produtos/cobranças diferentes, salvo mudança oficial futura;
- para produto próprio, planejar configuração segura por chave de API ou mecanismo oficialmente suportado;
- nunca embutir chaves no código;
- armazenar segredos com mecanismo seguro do Windows;
- mostrar estimativa/registro de uso;
- exigir supervisão em ações no AutoCAD e Builder;
- consultar documentação oficial atual antes de implementar autenticação ou Computer Use.

---

## 6. Situação do ambiente de desenvolvimento

Uma verificação somente de leitura encontrou:

| Item | Situação encontrada |
|---|---|
| Git | Funciona apenas pela distribuição interna do Codex |
| Git encontrado | `2.53.0.windows.3` |
| Caminho do Git interno | `C:\Users\danil\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe` |
| .NET 8 Runtime | Instalado, versão/host 8.0.8 |
| .NET 8 SDK | Não estava instalado |
| Visual Studio Community | Não estava instalado |
| Workload desktop .NET | Não estava instalado |

Instalações solicitadas ao Codex:

- Git oficial para Windows;
- .NET 8 SDK x64 estável;
- Visual Studio Community estável;
- workload `Microsoft.VisualStudio.Workload.ManagedDesktop`;
- WPF;
- Windows SDK;
- MSBuild.

Preferência:

- instalar no disco D tudo que o instalador oficial permitir;
- aceitar que .NET SDK, Windows SDK, componentes compartilhados e arquivos de sistema permaneçam no C quando obrigatório;
- não usar junctions ou soluções não suportadas;
- não reiniciar automaticamente;
- não instalar ferramentas dentro de `D:\Projetos\AutoAIBuilder`.

Antes de iniciar o desenvolvimento, o Codex deve repetir a verificação e confirmar que o ambiente está aprovado.

---

## 7. Automação de preparo de máscaras — objetivo

Fluxo de entrada:

- receber um projeto arquitetônico DWG;
- remover conteúdo dispensável;
- preservar a arquitetura necessária;
- identificar semanticamente pontos elétricos e hidrossanitários;
- organizar os itens em layers semânticas;
- manter blocos quando sua integridade for útil;
- exportar mapas de coordenadas e auditoria;
- gerar uma máscara adequada para o Builder.

Conteúdo normalmente removível:

- vegetação;
- veículos;
- mobiliário desnecessário;
- cotas;
- carimbos;
- textos e informações extras;
- vistas e detalhes genéricos dispensáveis.

Conteúdo a preservar:

- paredes;
- portas;
- janelas;
- elementos arquitetônicos necessários;
- pontos elétricos;
- pontos hidráulicos;
- pontos sanitários aplicáveis;
- textos associados relevantes, como `20A` e `220V`;
- blocos e cores úteis à identificação.

Decisão importante:

Não transformar tudo em cinza nem explodir indiscriminadamente. Layers, blocos, cores, legenda e geometria original fornecem informação útil à automação.

Foi decidido não ler o conteúdo interno de `CINZA PONTOS`, porque o custo de processamento e desenvolvimento seria alto em relação ao ganho esperado. Esse conjunto deve permanecer intacto.

---

## 8. Arquivo de teste e dados históricos

Projeto de teste:

- AutoCAD 2025 completo;
- aproximadamente 15.400 objetos no desenho original;
- máscara com pontos elétricos e hidráulicos/sanitários;
- sem projeto luminotécnico completo;
- todos os pontos existentes no projeto original foram inicialmente preservados.

Classificação anterior:

- MANTER: 900;
- REMOVER: 448;
- REVISAR: 52.

Em uma análise v0.3.1:

- 1.400 objetos analisados;
- ELE: 311;
- HID: 119;
- SAN: 2;
- ARQ_BASE: 2;
- MOBILIÁRIO: 118;
- CONTEXTO: 354;
- DESCARTÁVEIS: 447;
- REVISAR: 47.

Observações:

- os 2 sanitários eram de um detalhe genérico, não pontos necessários da planta baixa;
- vistas A, B, C, D e seus marcadores podem ser descartados;
- vegetação, carros e algumas legendas estavam dentro de bloco arquitetônico e não foram removidos naquela etapa;
- não se recomendou explodir indiscriminadamente esses blocos.

---

## 9. Evolução das rotinas AutoCAD

Os nomes abaixo representam versões experimentais. O Codex deve localizar os arquivos reais antes de continuar.

### v0.3 / v0.3.1

Comandos observados:

- `MASCARA_V03_ANALISAR`
- `MASCARA_V03_PONTOS`
- `MASCARA_V03_LIMPAR`
- `MASCARA_V03_ELETRICO`
- `MASCARA_V03_HIDRAULICO`
- `MASCARA_V03_SANITARIO`
- `MASCARA_V03_ARQUITETURA`
- `MASCARA_V03_DESCARTAVEIS`
- `MASCARA_V03_REVISAR`

Função:

- analisar objetos de topo;
- classificar por categorias;
- selecionar temporariamente os grupos;
- não alterar o desenho durante a análise.

Problema identificado:

- textos `20A` e `220V` associados a tomadas nem sempre acompanhavam o componente;
- alguns itens dispensáveis permaneciam em blocos arquitetônicos;
- detalhes genéricos eram confundidos com itens de instalação.

### v0.3.2

Arquivos gerados incluíram:

- componentes;
- itens de instalações;
- catálogo de blocos.

Validações informadas:

- componentes elétricos e hidráulicos foram selecionados corretamente;
- 72 textos recuperados eram elétricos, principalmente `220V` e `20A`;
- 49 detalhes associados eram dispensáveis;
- nenhuma mensagem de erro relevante.

Mapeamentos manuais de blocos:

| Bloco | Significado |
|---|---|
| `EGT` | Tomada média |
| `g4` | Tomada baixa |
| `FGWSG` | Ponto de som no teto |
| `5T4` | Ponto de internet RJ45 |
| `RFR` | Ralo linear de piso |
| `GH4EH4E` | Desviador de chuveiro |
| `frztg` | Ponto para pia de cozinha |

### v0.3.3 a v0.3.5 — leitura da legenda

Decisão:

Usar os quadros de simbologia do próprio projeto como fonte de verdade para identificar símbolos.

Comandos observados:

- `MASCARA_V033_LER_LEGENDA`
- `MASCARA_V033_ANALISAR`
- `MASCARA_V034_ANALISAR`
- `MASCARA_V035_ANALISAR`

Resultados v0.3.3:

- 316 objetos da legenda exportados;
- 54 blocos pareados automaticamente com textos;
- 342 blocos de topo analisados;
- 193 mapeados pelo dicionário/legenda;
- 1 classificado por regra;
- `CINZA PONTOS` permaneceu intacto.

Resultados v0.3.4:

- 280 blocos fora da legenda analisados;
- legenda/contexto: 248;
- ambíguos: 6;
- regras anteriores: 9;
- sem mapeamento: 17.

Resultados v0.3.5:

- 280 blocos fora da legenda analisados;
- legenda/contexto: 254;
- ambíguos: 0;
- regras anteriores: 26;
- sem mapeamento: 0.

Aprendizado:

- a legenda deve ser importada/lida em cada novo projeto;
- símbolos podem mudar entre arquitetos;
- o vínculo símbolo ↔ descrição deve ser registrado por projeto;
- a classificação não deve depender somente do nome aleatório do bloco.

### v0.4

Comandos observados:

- `MASCARA_V04_PREPARAR`
- `MASCARA_V04_ELETRICO`
- `MASCARA_V04_HIDRAULICO`
- `MASCARA_V04_BASE`
- `MASCARA_V04_DESCARTAVEIS`

Pré-visualização v0.4.2:

- pontos finais: 272;
- elétricos: 196;
- hidráulicos: 76;
- associados seguros: 100;
- `ELE_GRAF`: 24;
- `ELE_TEXTO`: 70;
- `HID_GRAF`: 6;
- baixa confiança excluídos: 59;
- base/contexto: 3;
- detalhes fora da legenda: 5;
- objetos selecionados na legenda: 202.

Comportamento:

- análise/seleção temporária;
- desenho não modificado;
- `CINZA PONTOS` intacto.

Correção posterior:

- tomadas duplas precisavam selecionar ambos os triângulos;
- textos `20A` e `220V` precisavam acompanhar a tomada;
- uma rotina inicialmente demorava excessivamente e foi corrigida;
- o comando `MASCARA_V04_DESCARTAVEIS` não removeu todas as cotas; o usuário removeu as cotas manualmente e decidiu não priorizar essa correção naquele momento.

### v0.5

Comandos observados:

- `MASCARA_V05_PREVISUALIZAR`
- `MASCARA_V05_APLICAR_CAMADAS`

Resultado:

- 372 objetos selecionados temporariamente;
- 272 pontos principais;
- 100 componentes/textos associados;
- 35 layers semânticas criadas;
- 272 pontos movidos;
- 100 componentes/textos movidos;
- 0 erros de layer;
- nenhuma entidade apagada ou explodida;
- `CINZA PONTOS` intacto.

Exemplos de layers:

- `PONTOS_ELE_TOMADA_ALTA`
- `PONTOS_ELE_TOMADA_BAIXA`
- `PONTOS_ELE_TOMADA_MEDIA`
- `PONTOS_ELE_TOMADA_DUPLA_20A_220V`
- `PONTOS_ELE_TOMADA_DUPLA_CABECEIRA`
- `PONTOS_ELE_TOMADA_NO_TETO`
- `PONTOS_ELE_TOMADA_REDE_DADOS_LOGICA`
- `PONTOS_HID_CHUVEIRO`
- `PONTOS_HID_DESVIADOR_CHUVEIRO`
- `PONTOS_HID_DUCHA_HIGIENICA`
- `PONTOS_HID_LAVA_LOUCAS`
- `PONTOS_HID_RALO_LINEAR`
- `PONTOS_HID_TORNEIRA_DE_JARDIM`

### v0.6

O usuário informou que a execução ocorreu conforme orientado.

Uma máscara foi salva/importada com nome semelhante a:

`TESTE_01_MASCARA_AUTOMATICA_V06`

Essa versão contém as layers semânticas aplicadas e foi utilizada nos testes do Builder.

### v0.7

Comandos observados:

- `MASCARA_V07_STATUS`
- `MASCARA_V07_PREVISUALIZAR`
- `MASCARA_V07_EXPORTAR`

Auditoria:

- objetos semânticos organizados: 372/372;
- pontos principais: 272;
- elétricos: 196;
- hidráulicos: 76;
- componentes/textos associados: 100;
- total exportável: 372;
- layers semânticas: 35;
- descartáveis presentes: 0;
- base/contexto: 3;
- unidade do desenho `INSUNITS = 4`, milímetros;
- auditoria aprovada;
- `CINZA PONTOS` intacto.

Arquivos CSV gerados:

- pontos semânticos v07;
- componentes semânticos v07;
- auditoria da máscara v07.

Durante uma exportação:

- 272 pontos exportados;
- 100 componentes/textos exportados;
- 0 ignorados;
- nenhuma entidade do DWG alterada.

### v0.8 — direções e calibração para o Builder

Comandos observados:

- `MASCARA_V08_CALIBRAR`
- `MASCARA_V08_EXPORTAR`

Calibração inicial:

- tomada média voltada para baixo:
  - bloco `EGT`;
  - handle `21AF02F`;
  - rotação aproximada `90.02296445°`;
  - escala 1,1,1;
- tomada média voltada para esquerda:
  - bloco `EGT`;
  - handle `2AFD76`;
  - rotação aproximada `219.31025869°`;
  - escala 1,1,1.

O segundo símbolo não era horizontal exato: era uma representação inclinada para sudoeste para não coincidir visualmente com o interruptor.

Regra:

- reconhecer orientação geométrica real;
- permitir direções cardeais e inclinadas;
- usar 0°, 90°, 180° e 270° somente para símbolos efetivamente horizontais/verticais;
- símbolos para esquerda/direita podem ter inclinação para cima ou para baixo;
- em caso ambíguo, pedir confirmação ao usuário;
- registrar ângulo bruto e direção classificada;
- não reduzir prematuramente toda rotação a quatro direções.

Foi gerado um CSV de diagnóstico de direções v08/v081.

---

## 10. Importação manual no AltoQi Builder

Decisão:

Por enquanto, o usuário fará manualmente:

1. criar a edificação;
2. escolher nome e pasta;
3. aceitar/definir norma;
4. manter somente as disciplinas:
   - Fiação;
   - Hidráulico;
   - Sanitário;
5. configurar propriedades;
6. cadastrar pavimentos;
7. finalizar a organização;
8. importar o DWG da máscara no pavimento.

No diálogo de importação:

- navegar até a pasta da máscara;
- trocar o filtro de `.cad` para `.dwg`;
- selecionar o arquivo.

A máscara entra como desenho de referência no pavimento.

Observação:

- na aba de Fiação, a geometria da máscara fica travada como referência;
- não é possível clicar diretamente no triângulo da tomada nessa aba;
- é necessário abrir o desenho da máscara em sua própria guia dentro do Builder para selecionar entidades e verificar a layer;
- nessa guia foi possível confirmar `PONTOS_ELE_TOMADA_MEDIA`.

---

## 11. Testes de lançamento no Builder

### 11.1 Sequência operacional observada

Antes de lançar qualquer ponto:

1. abrir o projeto de Fiação do pavimento;
2. conferir a aba `Lançamento`;
3. conferir a rede `Elétrica`;
4. conferir a altura/posição:
   - baixa;
   - média;
   - alta;
   - teto;
   - piso;
   - direita, quando aplicável;
5. somente depois selecionar o comando/ponto;
6. escolher a peça correta;
7. aplicar ponto relativo;
8. clicar na referência;
9. indicar a orientação;
10. validar visualmente.

Falha humana recorrente nos testes:

O usuário esqueceu algumas vezes de ajustar `Posição = Média` antes do lançamento. Portanto, a automação deve sempre ler/verificar explicitamente a posição antes de qualquer clique.

### 11.2 Peças

Para tomada média de 20 A, foi usada uma peça semelhante a:

`Pontos de força - Uso geral - 2P+T 20 A - média`

Regra de projeto do usuário:

- tomadas comuns são lançadas como 10 A ou 20 A;
- não selecionar peças de potência 2200 W apenas por causa da carga;
- potências/circuitos especiais são ajustados posteriormente nos circuitos e no dimensionamento da fiação;
- a escolha 10 A versus 20 A deve respeitar a indicação real da máscara/projeto;
- no teste da área de serviço foi usada uma tomada 20 A apenas para manter o padrão do experimento, embora o correto para aquele ponto fosse 10 A.

### 11.3 Ponto relativo e deslocamentos

O ponto gráfico da máscara não coincide diretamente com o ponto de inserção da peça do Builder. Foi necessário usar o recurso `Ponto relativo`.

Valores geométricos básicos testados:

- diferença de 1,5 cm em um eixo;
- diferença de 2,4 cm no outro eixo.

Os sinais dependem da orientação.

Casos validados inicialmente:

- tomada orientada para baixo;
- tomada orientada para esquerda.

Exemplo registrado para orientação à esquerda:

`2.4,-1.5,0`

Mas atenção: houve ajustes adicionais de origem/offset global durante os testes. Portanto, esses valores não podem ser aplicados cegamente a todo o desenho.

### 11.4 Offset global observado

Em um teste, a tomada ficou:

- 23,7 cm para a esquerda;
- 25,2 cm para baixo.

Foi aplicada uma correção de coordenadas.

Depois ainda ficou:

- 1,5 cm para cima;
- 2,4 cm para a esquerda.

Após nova correção, o usuário declarou:

`Lançamento perfeito`.

Em outro ponto, uma primeira tentativa ficou deslocada e uma correção posterior foi aprovada como correta.

Conclusão:

Há pelo menos duas transformações distintas:

1. transformação global entre coordenadas/máscara e Builder;
2. deslocamento local do ponto de inserção da peça, dependente da orientação.

Não misturar as duas.

Modelo conceitual:

```text
P_builder =
    TransformacaoGlobal(P_mascara)
    + OffsetLocalDaPeca(orientacao, tipo, altura)
```

A transformação deve ser calibrada com pontos de controle, e não deduzida de um único lançamento.

### 11.5 Orientações

Casos para baixo e esquerda funcionaram após calibração.

O usuário autorizou pular a validação imediata de cima e direita, considerando-as inversões dos sinais. Porém isso ainda deve ser validado antes de automação em produção.

Tomadas podem ter:

- esquerda exata;
- esquerda inclinada para cima;
- esquerda inclinada para baixo;
- direita exata;
- direita inclinada para cima;
- direita inclinada para baixo;
- cima;
- baixo.

Não inferir direção apenas pelo quadrante sem tolerância angular e sem preservar o ângulo.

---

## 12. Regras semânticas já consolidadas

1. Usar conjuntamente:
   - layer;
   - bloco;
   - geometria;
   - cor;
   - textos próximos;
   - legenda;
   - contexto espacial.
2. A legenda do próprio projeto tem prioridade para definir a simbologia.
3. Nomes de blocos podem ser aleatórios e não são suficientes isoladamente.
4. Textos `20A`, `220V`, alturas e observações precisam ser associados ao símbolo correto.
5. Tomada dupla deve manter os dois triângulos e seus textos associados.
6. Cores e blocos originais devem ser preservados quando fornecem informação.
7. Não explodir tudo.
8. Não ler o interior de `CINZA PONTOS`.
9. Detalhes, vistas e legendas fora da planta não devem virar pontos de instalações.
10. A classificação precisa registrar confiança e origem da decisão.
11. Itens de baixa confiança devem ir para revisão humana.
12. Toda alteração no DWG deve ser precedida por pré-visualização.
13. Nenhuma rotina deve apagar automaticamente sem auditoria e confirmação.

---

## 13. Arquivos CSV históricos mencionados

O Codex deve procurar os arquivos reais, principalmente nas pastas de trabalho do AutoCAD e do projeto. Nomes observados:

- `TESTE_01_ARQUITETURA_ORIGINAL_analise_mascara(1).csv`
- `TESTE_01_MASCARA_MANUAL_analise_mascara(1).csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_classificacao_mascara_v02.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_mapa_semantico_v03.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_componentes_v032.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_itens_instalacoes_v032.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_catalogo_blocos_v032.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_legenda_pares_v033.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_catalogo_semantico_v033.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_itens_semanticos_v033.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_legenda_objetos_v033.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_dicionario_legenda_v034.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_itens_semanticos_v034.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_catalogo_semantico_v034.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_dicionario_legenda_v035.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_itens_semanticos_v035.csv`
- `TESTE_01_ARQUITETURA_ORIGINAL_catalogo_semantico_v035.csv`
- `TESTE_01_MASCARA_AUTOMATICA_V06_componentes_semanticos_v07.csv`
- `TESTE_01_MASCARA_AUTOMATICA_V06_pontos_semanticos_v07.csv`
- `TESTE_01_MASCARA_AUTOMATICA_V06_auditoria_mascara_v07.csv`
- `TESTE_01_MASCARA_AUTOMATICA_V06_diagnostico_direcoes_v08.csv`
- `TESTE_01_MASCARA_AUTOMATICA_V06_diagnostico_direcoes_v081.csv`

Possível pasta histórica observada:

`C:\Users\danil\OneDrive\Documentos\Automacao_CAD\02_Trabalho`

Confirmar o caminho real; não mover arquivos sem autorização.

---

## 14. Requisitos de segurança e supervisão

O AutoAIBuilder deve operar em níveis:

### Somente leitura

- analisar arquivos;
- ler layers, blocos, textos e coordenadas;
- gerar relatórios;
- criar pré-visualizações.

### Alteração reversível

- criar layers;
- mover objetos entre layers;
- gerar cópias;
- adicionar marcações;
- manter histórico e desfazer.

### Ação supervisionada

- apagar objetos;
- lançar pontos no Builder;
- executar cliques;
- importar arquivos;
- alterar propriedades de peças;
- gerar circuitos;
- salvar/exportar projetos.

Requisitos mínimos para ações supervisionadas:

- mostrar plano antes de executar;
- exibir contagem e itens afetados;
- trabalhar em cópia;
- checkpoint;
- log;
- possibilidade de cancelar;
- relatório pós-execução;
- validação visual.

O software não substitui a responsabilidade técnica do engenheiro.

---

## 15. Arquitetura funcional sugerida

Separar claramente:

### Core

- entidades do domínio;
- tipos de pontos;
- orientação;
- coordenadas;
- unidades;
- confiança;
- regras;
- auditoria.

### Importação CAD

- leitura de CSVs existentes;
- futuramente leitura direta de DWG/DXF por mecanismo compatível;
- catálogo de layers;
- catálogo de blocos;
- textos;
- bounding boxes;
- unidades;
- transformação de coordenadas.

### Motor semântico

- dicionário da legenda;
- regras;
- associação de textos;
- agrupamento de símbolos;
- classificação;
- confiança;
- fila de revisão.

### Automação AutoCAD

- encapsular as rotinas AutoLISP existentes;
- execução com parâmetros;
- captura de resultados;
- logs;
- nunca depender somente de cliques quando houver API/AutoLISP apropriada.

### Automação Builder

- inicialmente protótipo supervisionado;
- captura de tela/Computer Use;
- calibração;
- verificação de estado da interface;
- escolha de peça;
- altura/posição;
- deslocamento relativo;
- direção;
- confirmação pós-lançamento.

### Interface

- dashboard;
- projetos;
- visualizador;
- layers;
- galeria de itens;
- revisão;
- logs;
- configurações;
- execução por etapas.

### Infraestrutura

- persistência local;
- armazenamento de projetos;
- configuração segura de credenciais;
- telemetria local opcional;
- atualizações futuras;
- exportação de auditoria.

---

## 16. Modelo de dados mínimo sugerido

```text
Project
  Id
  Name
  RootPath
  SourceDwgPath
  MaskDwgPath
  DrawingUnits
  CreatedAt
  UpdatedAt

SemanticItem
  Id
  ProjectId
  SourceHandle
  SourceLayer
  SourceBlockName
  EffectiveBlockName
  Category
  Discipline
  SemanticType
  PositionX
  PositionY
  PositionZ
  RotationDegrees
  ScaleX
  ScaleY
  ScaleZ
  Confidence
  DecisionSource
  ReviewStatus

AssociatedComponent
  Id
  SemanticItemId
  ComponentType
  Text
  SourceHandle
  RelativeX
  RelativeY
  Confidence

LegendDefinition
  Id
  ProjectId
  SymbolSignature
  Description
  SemanticType
  HeightCm
  CurrentA
  VoltageV

BuilderPlacementRule
  Id
  SemanticType
  BuilderPiece
  BuilderPosition
  NominalHeightCm
  OrientationClass
  LocalOffsetX
  LocalOffsetY
  LocalOffsetZ

CoordinateCalibration
  Id
  ProjectId
  SourcePointX
  SourcePointY
  TargetPointX
  TargetPointY
  TransformationType
  ResidualError

AuditEvent
  Id
  ProjectId
  Timestamp
  Action
  Input
  Output
  Status
  Message
```

Usar tipos fortes para unidades. Evitar misturar milímetros, centímetros e coordenadas de tela.

---

## 17. Primeiro escopo recomendado da plataforma

Não começar automatizando tudo.

### MVP 0.1

1. Solução .NET 8 WPF compilando.
2. Shell visual com tema inspirado no mockup.
3. Cadastro local de projeto.
4. Seleção da pasta do projeto.
5. Importação dos CSVs v07/v08.
6. Lista de pontos semânticos.
7. Filtros por disciplina, layer e tipo.
8. Painel de detalhes.
9. Galeria de revisão.
10. Status aprovado/revisar/ignorar.
11. Log local.
12. Nenhuma automação do Builder nesta versão.

### MVP 0.2

1. Visualização simplificada por coordenadas.
2. Associação símbolo ↔ texto.
3. edição de classificação;
4. exportação do CSV revisado;
5. importação da auditoria;
6. comparação antes/depois.

### MVP 0.3

1. Integração controlada com as rotinas AutoCAD existentes.
2. Pré-visualização.
3. Execução em cópia.
4. leitura dos resultados.

### Etapa posterior

- protótipo de um único tipo de lançamento no Builder;
- tomada média;
- uma área de teste;
- confirmação humana a cada ponto;
- depois lote supervisionado.

---

## 18. Próximos passos imediatos para o Codex

1. Verificar ambiente de desenvolvimento.
2. Inspecionar `D:\Projetos\AutoAIBuilder`.
3. Inventariar arquivos existentes sem alterá-los.
4. Procurar `AGENTS.md`, README, solução, projetos e Git.
5. Procurar os AutoLISP e CSVs históricos nos caminhos informados.
6. Produzir um relatório de inventário.
7. Propor a estrutura da solução WPF.
8. Confirmar com o usuário antes de criar a solução se houver arquivos conflitantes.
9. Criar o MVP 0.1 em pequenos commits.
10. Manter documentação em `docs`.

Critérios para considerar o ambiente pronto:

- Git oficial acessível;
- .NET 8 SDK listado;
- Visual Studio Community instalado;
- workload desktop .NET instalado;
- WPF compila;
- Windows SDK presente;
- pasta do projeto no D acessível.

---

## 19. Prompt inicial recomendado no Codex

```text
Leia integralmente o arquivo AutoAIBuilder_Contexto_Transferencia_Codex.md.

Use-o como contexto do projeto, mas confirme todas as informações técnicas
inspecionando os arquivos reais do computador. Não altere nada inicialmente.

Faça uma auditoria somente de leitura:

1. verifique o ambiente Git, .NET 8 SDK, Visual Studio, WPF, MSBuild e Windows SDK;
2. inspecione D:\Projetos\AutoAIBuilder;
3. procure a solução, projetos, README, AGENTS.md e arquivos existentes;
4. inventarie AutoLISP, CSV, DWG e documentação relevantes;
5. procure, sem mover, os artefatos históricos em
   C:\Users\danil\OneDrive\Documentos\Automacao_CAD\02_Trabalho;
6. compare os arquivos encontrados com os nomes citados no documento;
7. indique o que está disponível, ausente ou divergente;
8. proponha o plano do MVP 0.1;
9. não instale, não apague, não mova e não edite nada nessa primeira auditoria.

Entregue uma tabela de inventário, riscos, dependências e próximos passos.
```

---

## 20. Pontos que ainda precisam ser confirmados

- localização atual dos arquivos AutoLISP;
- versão mais recente real das rotinas;
- se v0.8 exportou todos os campos necessários;
- transformação global final entre DWG e Builder;
- convenção completa de sinais para todas as direções;
- comportamento para cima/direita;
- tolerância angular;
- correspondência entre orientação visual e orientação de inserção da peça;
- regra para 10 A versus 20 A por tipo de ponto;
- tratamento de tomadas duplas;
- importação de dados pela plataforma;
- tecnologia de visualização do DWG;
- mecanismo oficial disponível para automação do Builder;
- estratégia oficial de OpenAI API/Computer Use;
- requisitos comerciais, licenciamento e distribuição.

---

## 21. Resumo executivo

O trabalho já provou que é possível:

- classificar a arquitetura;
- ler a legenda;
- criar dicionário por projeto;
- identificar 272 pontos principais;
- associar 100 componentes/textos;
- organizar 372 objetos em 35 layers semânticas;
- exportar coordenadas e auditoria;
- preservar `CINZA PONTOS`;
- importar a máscara no Builder;
- identificar layers dentro da guia do desenho;
- lançar tomadas com deslocamento relativo;
- corrigir transformação e offset até obter coincidência visual.

O que ainda não está pronto:

- automação geral e robusta do Builder;
- calibração universal;
- lançamento em produção;
- validação de todos os tipos de pontos;
- plataforma desktop implementada;
- integração segura com IA;
- empacotamento comercial.

A direção recomendada é transformar os resultados atuais em uma base de dados e interface de revisão antes de ampliar as ações automáticas.

---

## 22. Decisão de produto após a validação do Marco 11.6H — 29/07/2026

Esta seção é a orientação vigente e prevalece sobre recomendações anteriores
de limpeza automática da arquitetura.

### Mudança de premissa

O núcleo inicial do AutoAIBuilder será:

- identificar pontos elétricos, hidráulicos e de iluminação;
- classificar corretamente cada ponto;
- preservar coordenadas, unidade, altura, rotação e orientação;
- associar textos e componentes relevantes;
- permitir revisão humana;
- gerar um plano confiável para lançamento supervisionado no AltoQi Builder.

A limpeza de cotas, vegetação, carros, mobiliário, layers dispensáveis e outros
elementos arquitetônicos deixa de fazer parte do caminho crítico. Essa limpeza
será manual neste primeiro ciclo do produto.

### Resultado real do 11.6H

A execução comprovou:

- 272/272 pontos na layer semântica correta;
- 100/100 componentes associados;
- preservação dos textos `220V`, `20A` e equivalentes;
- integridade do DWG original e da máscara histórica;
- execução isolada sobre cópia técnica.

Entretanto, o resultado não foi aprovado como máscara visual completa. Uma
referência `INSERT` chamada `CINZA PONTOS`, na layer `CHAMADA - cotas`, estava
classificada como `REMOVER`. O bloco reunia portas, janelas, mobiliário,
bancadas, cubas e eletrodomésticos. A remoção da referência eliminou todo esse
conteúdo da cópia. Muitas cotas, por outro lado, permaneceram.

Conclusão oficial:

- identificação semântica: **APROVADA**;
- limpeza arquitetônica: **REPROVADA**;
- DWG resultante: evidência técnica, não arquivo de produção.

### Política vigente de preservação

Até decisão futura expressa:

- não apagar entidades arquitetônicas;
- não apagar layers;
- não executar limpeza por classificação `REMOVER`;
- não explodir blocos;
- não remover `INSERT`, bloco dinâmico ou referência externa;
- não alterar geometria arquitetônica;
- trabalhar sempre sobre cópia técnica;
- permitir alterações apenas nos pontos e componentes explicitamente
  identificados e aprovados.

A cor das layers semânticas é apenas visual e não constitui requisito
funcional. O requisito é a correção do tipo, coordenada, altura, rotação,
orientação, pavimento, unidade e vínculo com a entidade de origem.

### Próxima etapa única

Executar **11.6H.1 — Modo de preservação total**:

1. retirar a limpeza automática do executor;
2. remover o catálogo de classificação v02 dos portões obrigatórios;
3. impedir qualquer chamada de exclusão no adaptador AutoLISP;
4. manter a organização dos pontos e componentes aprovados;
5. atualizar interface, manifesto, métricas, testes e documentação;
6. repetir o teste sobre nova cópia do mesmo DWG;
7. confirmar 272/272 pontos, 100/100 componentes e nenhuma entidade
   arquitetônica removida.

Somente depois do 11.6H.1 será iniciado o reconhecimento de um DWG nunca
analisado.

### Resultado real do 11.6H.1

O marco foi implementado e validado em 29/07/2026:

- perfil executado: `PRESERVATION_TOTAL_11.6H.1`;
- 272/272 pontos corretos;
- 100/100 componentes corretos;
- 272/272 correspondências com a máscara histórica;
- 1.400 entidades antes e 1.400 depois;
- zero handles ausentes;
- bloco `CINZA PONTOS`: 1 referência antes e 1 depois;
- original e referência histórica com SHA-256 inalterados;
- adaptador executado sem `entdel`;
- 131 testes automatizados aprovados.

As rotinas históricas permanecem arquivadas na execução apenas para
rastreabilidade e não são carregadas pelo script 11.6H.1. O resultado aprovado
é `AUTOAIBUILDER_PONTOS_PRESERVADOS_20260729_180103.dwg`.

O próximo marco deve iniciar o reconhecimento supervisionado de um DWG nunca
analisado, sem depender dos handles históricos deste projeto de teste.

---

## 23. Marco 11.6I — Reconhecimento supervisionado

Implementado em 29/07/2026.

O novo módulo `Reconhecer novo DWG`:

- recebe um DWG escolhido pelo usuário;
- cria e valida uma cópia técnica;
- usa o AutoCAD Core Console somente para leitura;
- inventaria entidades, blocos, handles, layers, posições, rotações e escalas;
- compara os blocos com os 272 pontos da base semântica validada;
- atribui confiança e explica o motivo da proposta;
- exibe candidatos sobre a planta;
- permite aprovar, rejeitar e corrigir;
- persiste cada revisão em auditoria;
- confirma novamente o SHA-256 do original;
- não contém comandos de mutação do desenho.

Validação do mecanismo:

- 1.400 entidades inventariadas;
- 342 referências de bloco `INSERT`;
- original íntegro;
- 136 testes automatizados aprovados.
- validação calibrada real concluída com 1.400 entidades, 342 blocos `INSERT`,
  245 candidatos em alta confiança e 97 encaminhados para revisão;
- os 272 handles históricos foram usados para calibrar a precisão sem alterar
  o DWG original.
- um DWG arquitetônico inédito foi processado com 9.094 entidades, 139 blocos
  candidatos, 8.055 primitivas gráficas e 88,57% de cobertura;
- nenhum dos 139 candidatos recebeu alta confiança, pois o arquivo usa blocos
  anônimos `A$C...` e vocabulário diferente da base histórica;
- a execução inédita corrigiu um laço de codificação do caractere `%` nos
  exportadores de inventário e de visualização.

Limite vigente: a primeira entrega detecta blocos `INSERT` do Model Space.
Símbolos feitos apenas de linhas soltas ou profundamente aninhados em blocos
compostos serão tratados a partir dos resultados do primeiro DWG realmente
novo. Portanto, o marco está implementado, mas sua validação de campo depende
do arquivo novo que será escolhido pelo usuário.
