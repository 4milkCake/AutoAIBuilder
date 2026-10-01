# Marco 11.6C — Ponte semântica e revisão inicial

## Resultado

O AutoAIBuilder agora recebe os relatórios estruturados produzidos pelas rotinas
históricas do AutoCAD e os transforma em dados internos consultáveis. A
importação é somente de leitura: os CSVs e o DWG referenciado não são movidos,
copiados, executados ou alterados.

O módulo **Análise semântica** permite:

- selecionar em conjunto o CSV de pontos v07, componentes v07 e direções v081;
- reconhecer o tipo de cada arquivo pelo cabeçalho, não apenas pelo nome;
- ler arquivos UTF-8 e a codificação legada usada pelos relatórios do AutoCAD;
- validar colunas, quantidade de campos, IDs duplicados, referências entre
  componentes e pontos e consistência do DWG de origem;
- calcular SHA-256 de cada origem e um fingerprint combinado do conjunto;
- persistir pontos, componentes e diagnósticos no SQLite;
- pesquisar e filtrar pontos elétricos, hidráulicos, aprovados ou pendentes;
- visualizar uma prévia espacial normalizada pelas coordenadas de centro;
- aprovar um ponto ou marcá-lo para revisão sem alterar o desenho;
- preservar as decisões humanas quando o mesmo conjunto é reimportado.

## Linha de base histórica

A regressão automatizada confere o conjunto validado no trabalho anterior:

| Métrica | Valor esperado |
|---|---:|
| Pontos | 272 |
| Elétricos | 196 |
| Hidráulicos | 76 |
| Componentes | 100 |
| Camadas semânticas | 35 |
| Diagnósticos de direção | 38 |
| Direções para revisão | 2 |
| Deslocamentos validados no Builder | 15 |
| Deslocamentos inferidos por simetria | 23 |

O selo **LINHA DE BASE HISTÓRICA APROVADA** somente aparece quando todos esses
valores são reproduzidos simultaneamente.

O teste de regressão utiliza os artefatos históricos quando eles estão
disponíveis na pasta `Automacao_CAD`. A suíte também contém casos sintéticos
portáveis para validar persistência, reimportação, codificação legada, rejeição
de referências órfãs e preservação dos arquivos de origem.

## Persistência e rastreabilidade

O esquema SQLite 6 acrescenta:

- `SemanticDatasets`;
- `SemanticPoints`;
- `SemanticComponents`;
- `SemanticDirectionDiagnostics`.

Cada conjunto registra:

- projeto do AutoAIBuilder;
- nome e caminho informativo do DWG;
- versão da fonte;
- arquivos CSV selecionados;
- fingerprint SHA-256;
- totais importados;
- datas de primeira importação e atualização;
- resultado da comparação com a linha de base.

Alturas semânticas textuais, como `PISO` e
`A_CONFIRMAR_POR_CONTEXTO`, são preservadas. A representação numérica é
opcional e nunca substitui o valor original.

## Limites de segurança do marco

O 11.6C:

- não abre nem controla o AutoCAD;
- não executa AutoLISP;
- não interpreta um DWG diretamente;
- não gera ou aplica máscara;
- não controla o AltoQi Builder;
- não envia dados para serviços externos;
- não aplica aprendizado automático;
- não altera o arquivo arquitetônico.

Os estados de revisão pertencem ao projeto ativo e ficam registrados somente
no banco local do AutoAIBuilder.

## Evolução concluída no 11.6D

O Marco 11.6D acrescentou componentes e textos detalhados, fila de pendências,
revisão de direções, correções versionadas, desfazer, busca supervisionada de
semelhantes e o primeiro dicionário restrito ao projeto. Consulte
[semantic-review-advanced.md](semantic-review-advanced.md).
