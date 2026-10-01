# Rota atual do AutoAIBuilder

> **Mudança vigente em 30/07/2026:** o desenvolvimento funcional do aplicativo
> desktop foi pausado para validar primeiro as capacidades diretamente no
> Codex. O código e todos os marcos abaixo permanecem preservados como base
> técnica. A ordem atual de trabalho, as skills propostas e os portões de
> máscara, coordenadas e Builder estão em
> [Plano Codex-first do AutoAIBuilder](codex-first-plan.md).

Atualizado em 29/07/2026 após a execução real e aprovação técnica do 11.6H.1.

## Objetivo prioritário

Reconhecer pontos elétricos, hidráulicos e de iluminação em DWGs, preservar
tipo, coordenadas, unidade, altura, rotação, orientação e componentes
associados, permitir revisão humana e produzir um plano confiável de lançamento
para o AltoQi Builder.

A limpeza arquitetônica automática não faz parte do caminho crítico atual.
Cotas, vegetação, carros, mobiliário e layers dispensáveis serão tratados
manualmente pelo usuário nesta fase.

## Estado dos marcos

| Marco | Estado | Resultado |
|---|---|---|
| 11.6F | Concluído | Rotinas históricas catalogadas e vinculadas a contratos |
| 11.6G | Concluído | Prévia e aprovação supervisionada |
| 11.6H | Parcialmente aprovado | Semântica aprovada; limpeza visual reprovada |
| 11.6H.1 | Concluído | 272/272 pontos, 100/100 componentes e 1.400/1.400 entidades preservadas |
| 11.6I | Implementado; validação de campo pendente | Inventário somente leitura, confiança e revisão humana para DWG novo |
| 11.6I.1A | Concluído tecnicamente; validação visual do usuário pendente | Contrato WCS e correção auditável de pontos-base remotos |
| 11.6I.1B | Concluído tecnicamente; validação visual do usuário pendente | UTF-8, interpretação de MTEXT e posicionamento textual |
| Plano de lançamento | Posterior | Gerar dados neutros para o Builder |
| Builder supervisionado | Posterior | Lançar primeiro tipo de ponto em projeto de teste |

## Marco 11.6H.1 concluído

O executor foi restringido ao perfil `PRESERVATION_TOTAL_11.6H.1`:

- a limpeza automática foi retirada;
- o catálogo v02 deixou de ser requisito de execução;
- o adaptador executado não contém `entdel`;
- somente os handles semânticos aprovados mudam de layer;
- as rotinas históricas com limpeza são copiadas para auditoria, mas não são
  carregadas pelo script;
- todos os handles do Model Space são comparados antes e depois.

### Resultado real

- 272/272 pontos corretos;
- 100/100 componentes corretos;
- 1.400 entidades antes e 1.400 depois;
- zero handles ausentes;
- bloco `CINZA PONTOS`: 1 referência antes e 1 depois;
- original e referência histórica íntegros;
- execução aprovada no manifesto;
- 131 testes automatizados aprovados.

## Marco 11.6I implementado

O software agora recebe um DWG selecionado, gera um inventário somente leitura
em cópia técnica, compara os blocos com a base validada, atribui confiança e
permite aprovar, rejeitar ou corrigir cada candidato. O DWG original é
revalidado por SHA-256 e não existe comando de gravação nesta tela.

O mecanismo foi validado no desenho histórico, com 1.400 entidades e 342 blocos
`INSERT`. A conclusão funcional do marco depende agora de um ensaio de campo
com um arquivo realmente novo, para medir:

- quantidade real de pontos existentes;
- candidatos corretamente identificados;
- falsos positivos;
- pontos não encontrados;
- símbolos aninhados ou desenhados com linhas soltas.

Depois dessa medição serão implementadas as regras geométricas que o novo
arquivo demonstrar serem necessárias.

## Marco 11.6I.1A implementado

Desenho e marcadores agora compartilham o mesmo contrato WCS. O inventário
preserva o ponto de inserção original e calcula uma âncora geométrica alternativa
quando o ponto-base do bloco está fora da própria definição. No DWG que revelou
o defeito, 136 das 547 referências precisaram dessa correção e a quantidade de
marcadores dentro dos limites renderizados subiu de 338 para 422.

A próxima correção é o 11.6I.1B, dedicada a acentuação, comandos internos de
MTEXT, alinhamento e apresentação dos textos.

Após a validação visual do usuário, a tela do 11.6I também recebeu seleção
bidirecional completa: o clique no marcador revela e rola a fila até o
candidato. O visualizador ganhou mais espaço, divisor vertical ajustável,
controles diretos de enquadramento e uma janela ampliada maximizada.

## Marco 11.6I.1B implementado

O pipeline gráfico agora exporta texto em UTF-8 e preserva ancoragem, largura,
rotação, estilo e tipo original. O visualizador interpreta comandos internos de
MTEXT, quebras de linha, frações e símbolos técnicos antes de desenhar.

O ensaio real preservou corretamente os acentos dos 306 textos encontrados no
DWG e não produziu caracteres de substituição. A próxima etapa planejada é
11.6I.1C: localizar a legenda, associar símbolo e descrição e apresentar esse
dicionário local para confirmação do usuário.

## Marco 11.6I.1C implementado

O reconhecimento agora procura cabeçalhos explícitos `LEGENDA` e `SIMBOLOGIA`,
delimita uma faixa espacial proporcional à escala do desenho e propõe pares
entre blocos `INSERT` e descrições técnicas próximas. Os exemplares usados
dentro da legenda deixam de ser tratados como pontos reais da planta.

O dicionário local fica visível na própria tela, com bloco, descrição e
evidência. As ocorrências externas que usam um símbolo catalogado podem ser
filtradas por `Identificados pela legenda`. Nesta primeira leitura, nenhuma
associação da legenda recebe alta confiança automaticamente: o teto é 82% e a
revisão visual continua obrigatória. Blocos com descrições conflitantes ficam
ambíguos.

O ensaio somente leitura sobre o inventário real de
`Elétrico - Maíra e Pedro.dwg` encontrou três cabeçalhos, delimitou 187
entidades na região de legenda, selecionou 28 descrições técnicas e propôs 12
pares seguros. Outras 16 descrições permaneceram sem par, em vez de serem
associadas por força. Uma associação espacial contraditória entre o bloco
`PURIFICADOR` e uma descrição de fita de LED foi descartada pela barreira
semântica.

O próximo passo funcional é a validação visual do dicionário pelo usuário.
Depois dela, o 11.6I.1D poderá tratar símbolos aninhados, linhas soltas e
descrições que não possuem um `INSERT` pareável no nível superior.

## Marco 11.6I.1D implementado

O inventário técnico passou para o contrato `AAR|3`. O leitor agora expande
recursivamente blocos somente na cópia técnica, preserva o bloco raiz, a
profundidade, o caminho hierárquico e uma assinatura estrutural da geometria.
Essa identidade permite recuperar símbolos que estavam escondidos dentro de
blocos maiores sem confundir todos os elementos gráficos com pontos de projeto.

O centro geométrico real dos componentes expandidos também corrige blocos cuja
coordenada de inserção está distante do desenho visível. No ensaio de referência,
isso recuperou tomadas, interruptores, pontos especiais, gás e som que não podiam
ser pareados no 11.6I.1C. A entrada de gás e a saída de som passaram a usar
assinaturas distintas, eliminando a associação incorreta observada pelo usuário.

A interface informa a origem do reconhecimento, a assinatura e o caminho do
elemento. Todo resultado continua pendente de revisão: o marco amplia a cobertura,
mas não promove associações geométricas a alta confiança automaticamente.

O ensaio real, executado pelo AutoCAD Core Console sobre uma cópia de
`Elétrico - Maíra e Pedro.dwg`, produziu:

- 1.849 entidades originais e 7.572 entidades expandidas;
- 9.421 registros técnicos, dos quais 581 são inserções de bloco;
- 8.051 primitivas geométricas com limites utilizáveis;
- 40 descrições técnicas analisadas;
- 28 pares de legenda propostos e 12 descrições ainda sem par;
- 156 candidatos com âncora corrigida pela geometria expandida;
- 148 testes automatizados aprovados;
- zero comandos de gravação no DWG original.

As 12 descrições restantes, incluindo alguns símbolos desenhados de forma muito
particular, devem permanecer não classificadas até nova evidência. O próximo
passo é a conferência visual do usuário no próprio desenho e o registro dos
símbolos corretos, ausentes e incorretos antes de ampliar as regras.
