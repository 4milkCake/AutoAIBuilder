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
- serviços de arquivo e exportação: integração com diálogos e Explorer.

## Fronteira das automações

As automações futuras devem entrar pela interface
`IAutomationOrchestrator`. A interface recebe um `AutomationRequest`, publica
progresso e retorna `AutomationExecutionResult`.

Nenhuma implementação foi registrada nesta etapa. Portanto, o shell não
executa fluxos, CAD, máscaras ou agentes. A futura implementação poderá ficar
na Infrastructure sem depender da janela WPF.

## Diagnóstico e falhas

Eventos de ciclo de vida, avisos, erros e exceções globais são registrados sem
interromper a operação principal. A tela Diagnóstico exibe o estado do runtime,
os caminhos locais, a integridade dos JSON e os últimos eventos.

Um arquivo de projetos corrompido é apresentado como catálogo vazio somente
para permitir que o aplicativo abra, mas permanece protegido contra
sobrescrita até ser corrigido ou recuperado explicitamente.
