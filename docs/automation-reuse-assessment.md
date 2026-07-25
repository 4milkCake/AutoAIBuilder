# Marco 11.6C — Avaliação de reaproveitamento da automação existente

## Objetivo

O Marco 11.6C não deve recriar do zero o processo desenvolvido anteriormente
no ChatGPT Work. Seu primeiro objetivo é preservar a lógica já validada,
identificar as lacunas e adaptá-la à fronteira segura criada nos marcos 11.4 a
11.6B.

Esta avaliação é somente de leitura. Ela não importa máscaras, não executa
scripts, não controla aplicativos CAD e não altera arquivos de origem.

## Levantamento local realizado

Foram examinados:

- arquivos versionados e não versionados do repositório;
- nomes e conteúdo pesquisável dos arquivos, excluindo dependências e saídas de
  compilação;
- histórico Git disponível;
- catálogo e contratos atuais de automação;
- registro interno de adaptadores;
- pasta temporária `.tmp.driveupload`, apenas para identificação de natureza.

Resultado:

- não há exportação da conversa do ChatGPT Work;
- não há prompts funcionais da automação anterior;
- não há catálogo real de regras de tomadas, pontos elétricos ou hidráulicos;
- não há exemplos reais de entrada e saída;
- não há scripts, macros ou código da automação anterior;
- não há máscara real catalogada;
- o histórico Git contém somente a infraestrutura criada no AutoAIBuilder;
- `.tmp.driveupload` contém fragmentos temporários de compilação, MSBuild e
  restauração NuGet. Ela não é fonte da automação e permanece intacta e fora do
  Git.

Portanto, o conteúdo funcional da automação anterior ainda não está disponível
neste espaço de trabalho.

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
| Lógica funcional do ChatGPT Work | Ausente localmente | Deve ser fornecida e decomposta, sem reescrita prematura |
| Casos reais de regressão | Ausentes localmente | Devem ser fornecidos para comparar o novo resultado ao resultado já conhecido |
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

O AutoAIBuilder está tecnicamente preparado para receber o material e realizar
a análise. O início da conversão funcional depende apenas da disponibilização
dos artefatos produzidos no ChatGPT Work.

Até que esses artefatos sejam recebidos:

- nenhum adaptador real deve ser criado por suposição;
- nenhuma regra de tomada ou hidráulica deve ser inventada;
- nenhuma máscara deve ser considerada homologada;
- a execução de máscaras catalogadas deve permanecer bloqueada.

O pacote mínimo necessário está definido em
[Recepção da automação existente](automation-source-intake.md).
