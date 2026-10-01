# Marco 11.6C — Avaliação de reaproveitamento da automação existente

## Objetivo

O Marco 11.6C não deve recriar do zero o processo desenvolvido anteriormente
no ChatGPT Work. Seu primeiro objetivo é preservar a lógica já validada,
identificar as lacunas e adaptá-la à fronteira segura criada nos marcos 11.4 a
11.6B.

Esta avaliação começou somente em leitura. Após o recebimento do contexto
oficial e a inspeção dos artefatos reais, o Marco 11.6C avançou para a ponte
semântica documentada em [semantic-bridge.md](semantic-bridge.md). Ela importa
somente os CSVs selecionados para o banco local; não executa scripts, não
controla aplicativos CAD e não altera arquivos de origem.

## Levantamento local realizado

Foram examinados:

- arquivos versionados e não versionados do repositório;
- nomes e conteúdo pesquisável dos arquivos, excluindo dependências e saídas de
  compilação;
- histórico Git disponível;
- catálogo e contratos atuais de automação;
- registro interno de adaptadores;
- pasta temporária `.tmp.driveupload`, apenas para identificação de natureza.

Resultado atualizado:

- o contexto oficial de transferência está em
  `docs/AutoAIBuilder_Contexto_Transferencia_Codex.md`;
- as rotinas AutoLISP históricas foram localizadas na pasta `Automacao_CAD`;
- os CSVs reais v07/v081 foram localizados e inspecionados;
- a linha de base contém 272 pontos, 100 componentes, 35 camadas semânticas e
  38 diagnósticos de direção;
- 196 pontos são elétricos e 76 são hidráulicos;
- 2 direções precisam de revisão, 15 deslocamentos foram validados no Builder
  e 23 foram inferidos por simetria;
- os CSVs passam por regressão automatizada sem serem modificados;
- scripts e aplicativos CAD continuam fora da execução deste marco.

## Inventário do que já pode ser reutilizado

| Componente | Estado | Reaproveitamento no 11.6C |
|---|---|---|
| Esquema da máscara `1.0` | Pronto | Receber identidade, dependências, parâmetros, saídas e validações da automação existente |
| Esquema do catálogo de regras `1.0` | Pronto | Formalizar as regras de identificação e classificação já desenvolvidas |
| Normalização e SHA-256 | Pronto | Vincular cada revisão do material a um conteúdo imutável |
| Catálogo seguro de máscaras | Pronto | Armazenar a primeira máscara inicialmente inativa |
| Registro interno de adaptadores | Pronto | Associar a automação convertida por identidade, versão e hash exatos |
| Orquestrador seguro | Pronto | Revalidar contrato, regras e plano antes de qualquer simulação |
| Planejamento e idempotência | Pronto | Produzir um plano auditável e impedir repetição acidental |
| Área isolada e cópias verificadas | Pronto | Evitar que o adaptador receba ou altere arquivos originais |
| Auditoria SQLite | Pronto | Registrar avaliação, adaptador, contrato, hash e resultado |
| Piloto de cópia verificada | Pronto | Servir de referência técnica, não de substituto da automação real |
| Lógica funcional do ChatGPT Work | Contexto e artefatos localizados | Reaproveitada gradualmente, começando pelos dados estruturados v07/v081 |
| Casos reais de regressão | Disponíveis e auditados | Verificam automaticamente a linha de base 272/196/76/100/35/38/2 |
| Integração CAD | Não implementada | Será analisada separadamente; continua bloqueada nesta fase |

## Mapa de incorporação

O material anterior será convertido sem alterar sua intenção funcional:

| Material de origem | Destino no AutoAIBuilder |
|---|---|
| Etapas da conversa e sequência operacional | Fluxo versionado e responsabilidades do adaptador |
| Prompts e critérios de decisão | Regras identificadas, versionadas e rastreáveis |
| Regras de tomadas e pontos elétricos | Catálogo de regras por disciplina e severidade |
| Regras de pontos hidráulicos | Catálogo de regras por disciplina e severidade |
| Parâmetros escolhidos pelo usuário | Parâmetros declarativos da máscara |
| Pré-requisitos do desenho | Pré-condições bloqueantes ou informativas |
| Resultado esperado | Saídas e pós-condições verificáveis |
| Arquivos de exemplo | Casos de regressão somente leitura |
| Scripts ou macros | Fonte para revisão; nunca serão executados diretamente |
| Ações de interface no CAD | Capacidade futura isolada, não autorizada no 11.6C inicial |
| Correções feitas durante a conversa | Casos negativos e regras de exceção |

## Classificação que será aplicada

Cada trecho recebido terá uma das seguintes decisões:

1. **Reutilizar como está** — regra ou etapa já objetiva e testável.
2. **Normalizar** — conteúdo válido que precisa apenas de identidade, versão ou
   formato estruturado.
3. **Adaptar** — lógica válida, mas acoplada ao ChatGPT Work ou à interface.
4. **Completar** — comportamento conhecido com alguma condição ainda ausente.
5. **Manter humano** — decisão que ainda exige confirmação profissional.
6. **Bloquear** — operação destrutiva, não determinística ou sem evidência
   suficiente.
7. **Adiar integração** — ação CAD que não deve fazer parte da primeira
   simulação.

Nenhuma classificação autoriza execução. A homologação técnica continuará
separada da análise funcional.

## Sequência revisada do Marco 11.6C

### 11.6C.1 — Recepção e preservação

- receber exportações e anexos sem mover os originais;
- atribuir um identificador a cada artefato;
- registrar tamanho, data e SHA-256;
- separar documentação, exemplos, código e dados;
- rejeitar credenciais, tokens e dados pessoais desnecessários.

### 11.6C.2 — Decomposição funcional

- reconstruir a sequência real da automação;
- identificar entradas, decisões, saídas e intervenções humanas;
- separar máscara, identificação elétrica e identificação hidráulica;
- registrar exceções conhecidas e pontos ainda incompletos.

### 11.6C.3 — Matriz de reaproveitamento

- comparar o material com os contratos atuais;
- marcar o que será reutilizado, normalizado, adaptado ou bloqueado;
- apresentar lacunas antes de escrever o adaptador;
- obter uma linha de base dos resultados já aceitos pelo usuário.

### 11.6C.4 — Pacote declarativo candidato

- criar uma máscara candidata inicialmente inativa;
- criar o catálogo exato de regras;
- normalizar e calcular o SHA-256;
- validar referências, versões e dependências;
- não habilitar execução.

### 11.6C.5 — Adaptador de simulação

- incorporar somente a lógica homologada em código interno compilado;
- não carregar scripts, macros ou DLLs;
- produzir plano e resultado dentro da área isolada;
- manter aplicação real e controle CAD bloqueados.

### 11.6C.6 — Regressão e homologação

- executar sobre cópias de casos conhecidos;
- comparar o resultado com a linha de base;
- testar cancelamento, adulteração, timeout e idempotência;
- registrar divergências e aprovar apenas a simulação reproduzível.

## Estado atual da decisão

Os artefatos foram recebidos e a primeira conversão funcional foi concluída:
os CSVs v07/v081 alimentam o novo módulo de análise semântica. Nenhum adaptador
CAD real foi criado, nenhuma regra foi inventada por suposição e a execução de
máscaras catalogadas continua bloqueada. O passo seguinte é aprofundar a
revisão e registrar correções versionadas antes de qualquer geração de máscara.
