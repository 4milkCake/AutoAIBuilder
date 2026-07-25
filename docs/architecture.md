# Arquitetura do AutoAIBuilder

## Dependências

O fluxo de referências permanece unidirecional:

`Desktop → Application → Domain`

`Infrastructure → Application → Domain`

A camada Desktop compõe as implementações concretas em
`DesktopCompositionRoot`. A janela principal apenas solicita o ViewModel pronto
e não conhece persistência, relatórios ou serviços de domínio.

## Shell e módulos

O shell usa `INavigationService` como fonte única da seção ativa. Cada seção é
registrada como um `IWorkspaceModule`, com ciclo de ativação e atualização
próprio. Isso evita acrescentar novos `switch` à janela quando um módulo for
criado.

O `MainWindowViewModel` é parcial e está separado por responsabilidade:

- `MainWindowViewModel.Modules.cs`: navegação, ativação e notificações;
- `MainWindowViewModel.RulesAndSettings.cs`: regras e preferências;
- `MainWindowViewModel.Validation.cs`: validação preventiva;
- `MainWindowViewModel.Reports.cs`: geração e exportação;
- `MainWindowViewModel.History.cs`: histórico local;
- `MainWindowViewModel.Diagnostics.cs`: diagnóstico e execuções operacionais;
- `MainWindowViewModel.DataMaintenance.cs`: backup, restauração e realocação;
- `MainWindowViewModel.MaskCatalog.cs`: análise e catálogo declarativo de
  máscaras;
- `MainWindowViewModel.cs`: estado compartilhado, projetos, arquivos e painel.

Essa divisão preserva os bindings atuais enquanto permite extrair ViewModels
independentes gradualmente, sem uma reescrita arriscada da interface.

As telas de Relatórios e Histórico já são `UserControl`s próprios em `Views`.
As demais telas podem seguir o mesmo padrão sem alterar o contrato do shell.

## Serviços transversais

- `ActiveProjectContext`: identidade do projeto ativo;
- `INavigationService`: navegação sem referência ao WPF;
- `INotificationService`: mensagens de aplicação sem controles visuais;
- `IDialogService`: confirmações específicas da interface Windows;
- `IDiagnosticLogger`: eventos técnicos estruturados em JSON Lines;
- `IDiagnosticService`: fotografia verificável do ambiente e da persistência;
- `IOperationCoordinator`: ciclo assíncrono, cancelamento, timeout e exclusão
  mútua de operações;
- serviços de arquivo e exportação: integração com diálogos e Explorer.

## Motor operacional

`OperationCoordinator` é a fronteira de execução para tarefas demoradas. Cada
operação recebe identidade, tipo, nome, projeto opcional, recurso exclusivo e
timeout. O ciclo `Pending → Running → estado terminal` é persistido antes e
durante a execução.

Os estados terminais são `Succeeded`, `Cancelled`, `TimedOut`, `Failed` e
`Interrupted`. Exceções ficam associadas à execução e são registradas sem
escapar para a interface. Progresso nunca retrocede. Um recurso, como o banco de
dados local, aceita somente uma operação incompatível por vez.

Cancelamento e timeout são cooperativos. Se uma implementação demorar a observar
o token de cancelamento, o resultado é encerrado para a interface, mas o recurso
continua reservado até a tarefa subjacente terminar. Isso impede que uma segunda
operação conflitante seja iniciada sobre trabalho ainda ativo.

`AsyncCommand` protege o WPF contra reentrada, captura falhas por comando e
oferece cancelamento e timeout. Backup, restauração e realocação são os primeiros
consumidores reais desse motor. O piloto de cópia técnica também o utiliza, com
exclusão por projeto.

## Fronteira das automações

As automações futuras devem entrar pela interface
`IAutomationOrchestrator`. A interface recebe um `AutomationRequest`, publica
progresso e retorna `AutomationExecutionResult`.

O Marco 11.4 introduz uma fronteira anterior ao orquestrador. Catálogos de
regras e máscaras usam contratos `1.0`; `AutomationContractValidator` rejeita
esquemas desconhecidos, versões instáveis, identificadores, referências,
extensões e caminhos inseguros. `AutomationPlanService` valida entradas,
dependências e parâmetros, calcula SHA-256 e produz uma chave de idempotência.

`AutomationExecutionService` não entrega caminhos originais ao adaptador. Ele
cria cópias verificadas em uma área de staging, executa pré e pós-validadores,
confirma outra vez os checksums dos originais e só então publica uma pasta
exclusiva. Falhas e cancelamentos movem artefatos parciais para recuperação, sem
excluir conteúdo. Uma execução equivalente já concluída é reutilizada.

O único adaptador registrado é o piloto
`copia-tecnica-verificada@1.0.0`. Ele copia uma entrada isolada e gera um
manifesto; não interpreta o conteúdo, não abre ferramentas CAD e não representa
uma máscara definitiva. A tela exige simulação auditada e confirmação explícita
antes da aplicação. Consulte [Cópia técnica verificada](verified-copy-pilot.md).

O Marco 11.6A adiciona `AutomationMaskCatalogService` antes da fronteira de
execução. Ele lê dois JSONs locais com limite de tamanho, valida todos os campos
obrigatórios, normaliza os contratos, calcula SHA-256 e detecta repetição ou
conflito de conteúdo. `SqliteAutomationMaskCatalogRepository` mantém versões
lado a lado e garante no banco que apenas uma versão de cada identidade possa
estar ativa. Ativação não significa execução e nenhum adaptador é resolvido
pelo catálogo.

Máscaras gráficas, adaptadores CAD e agentes continuam desconectados.

## Diagnóstico e falhas

Eventos de ciclo de vida, avisos, erros e exceções globais são registrados sem
interromper a operação principal. A tela Diagnóstico exibe o estado do runtime,
os caminhos locais, a integridade do banco SQLite, as fontes legadas e os
últimos eventos e execuções operacionais.

O log JSON Lines tem tamanho máximo por arquivo, rotação numerada e quantidade
máxima de arquivos anteriores. Campos excessivos são limitados e propriedades
ou trechos identificados como senha, token, segredo, chave de API, autorização
ou credencial são removidos antes da serialização.

Um registro de projeto com conteúdo inválido é omitido do catálogo somente para
permitir que o aplicativo abra, mas o banco permanece protegido contra novas
gravações até que o conteúdo seja recuperado explicitamente.

## Persistência e recuperação

Os repositórios operacionais usam `Microsoft.Data.Sqlite` e um banco com versão
de esquema registrada por `PRAGMA user_version` e `SchemaMigrations`. Cada
operação de escrita é transacional, as conexões usam WAL e possuem tempo de
espera para contenção. O esquema 2 acrescenta `OperationExecutions`, que permite
recuperar o estado operacional depois de uma falha ou encerramento.

O esquema 3 acrescenta `AutomationAudits`, com contrato e versão da máscara,
snapshots SHA-256, modo, estado, chave de idempotência, saídas e caminhos de
publicação ou recuperação. Auditorias em andamento no encerramento são marcadas
como interrompidas no próximo início, preservando qualquer artefato existente.

O esquema 4 acrescenta `AutomationMaskCatalog`, com os contratos normalizados,
identidade e versão da máscara e das regras, SHA-256, origem resumida e estado
ativo ou inativo. A migração é aditiva e não altera projetos nem auditorias
existentes.

Os JSON das versões anteriores são tratados como fontes legadas. A migração é
idempotente, registra cada origem em `DataMigrations` e preserva os arquivos
originais sem renomear, mover ou excluir.

`AppState` persiste a identidade do projeto ativo. Backups usam a API de backup
online do SQLite; uma restauração sempre cria primeiro uma cópia automática do
estado atual e tenta retornar a ela caso a operação falhe.

A mudança de pasta de dados é agendada. O banco é validado no destino e, no
próximo início, uma cópia final do estado mais recente é criada antes que o novo
local seja ativado. A pasta anterior permanece intacta.
