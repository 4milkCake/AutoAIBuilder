# AutoAIBuilder

Aplicação desktop WPF para coordenar automações e agentes de IA aplicados a
projetos elétricos e hidrossanitários.

## Estrutura inicial

- `src/AutoAIBuilder.Desktop`: apresentação WPF e composição da aplicação.
- `src/AutoAIBuilder.Application`: contratos e casos de uso.
- `src/AutoAIBuilder.Domain`: entidades e regras centrais do produto.
- `src/AutoAIBuilder.Infrastructure`: integrações com arquivos, persistência e
  provedores externos.
- `tests/AutoAIBuilder.Tests`: testes MSTest do domínio e da persistência.

O SDK está fixado em .NET `8.0.423` pelo `global.json`. A imagem
`Generated image 1.png` é a referência visual inicial e permanece inalterada.

## Compilar

```powershell
dotnet restore AutoAIBuilder.sln --locked-mode
dotnet build AutoAIBuilder.sln --configuration Debug --no-restore
dotnet test AutoAIBuilder.sln --configuration Debug --no-build --no-restore
```

O `packages.lock.json` de cada projeto é versionado. Assim, a restauração local
e a integração contínua utilizam exatamente o mesmo grafo de dependências. O
workflow `.github/workflows/ci.yml` repete a sequência de restauração,
compilação e testes no Windows com o SDK definido em `global.json`.

## Estado atual

O shell permite navegar entre Painel, Projetos, Arquivos, Regras de projeto,
Validadores, Relatórios, Histórico e Configurações. O projeto selecionado
é tratado como projeto ativo e alimenta o cabeçalho, as métricas e a próxima ação
do painel. Projetos, configurações, histórico e o projeto ativo são persistidos
no banco SQLite versionado
`%LOCALAPPDATA%\AutoAIBuilder\Data\autoaibuilder.db`; arquivos são catalogados
por metadados e permanecem em seus locais originais.

O banco usa atualmente o esquema 2. Além dos dados funcionais, ele registra o
estado das execuções operacionais, incluindo progresso, timeout, cancelamento,
falha e conclusão. Execuções que estavam pendentes ou em andamento quando o
processo foi encerrado são recuperadas como interrompidas no próximo início.

Na primeira inicialização após a migração para SQLite, os antigos arquivos
`projects.json`, `settings.json` e `activity-log.json` são importados de forma
idempotente. Eles permanecem intactos como fonte legada e nunca são apagados
automaticamente.

A gestão de projetos permite criar, editar, duplicar, pesquisar, ordenar, arquivar
e restaurar espaços de trabalho. O arquivamento é reversível e a duplicação
preserva apenas referências de arquivos, sem copiar os originais.

O catálogo aceita seleção múltipla, pesquisa e filtros por tipo ou integridade.
Ele detecta arquivos ausentes ou alterados, permite atualizar metadados, abrir ou
localizar o original no Explorer e remover somente a referência mediante
confirmação.

As regras técnicas são persistidas por projeto e incluem unidade de medida,
escala, altura de pavimento, nomenclatura e critérios de bloqueio para validações
futuras. As configurações locais definem os valores padrão de novos projetos e a
confirmação de segurança para remoção de referências. Elas são armazenadas no
mesmo banco SQLite transacional.

Os validadores executam uma verificação preventiva e somente de leitura sobre
dados cadastrais, regras técnicas, integridade do catálogo e catálogo de camadas.
O resultado apresenta aprovações, alertas, erros e informa se as futuras
automações devem permanecer bloqueadas pelas regras do projeto.

O relatório de prontidão consolida dados, regras, arquivos e resultados dos
validadores em uma pré-visualização exportável como TXT, CSV ou PDF. TXT e PDF
contêm o diagnóstico completo; CSV contém as verificações em colunas próprias
para análise em planilhas. A exportação só grava depois da escolha explícita do
destino pelo usuário.

O histórico local registra ações importantes do aplicativo, permite pesquisa e
filtro por categoria e é limitado aos 500 eventos mais recentes. Ele também usa
o banco SQLite; falhas no histórico não interrompem a operação principal.

A tela Configurações permite criar backups consistentes, restaurá-los com uma
cópia automática do estado anterior e escolher oficialmente outra pasta de
dados. A realocação preserva o banco original, refaz a cópia final no próximo
início e nunca reinicia o computador ou o aplicativo automaticamente.

Essas operações de manutenção já usam o motor assíncrono do aplicativo. A
interface permanece responsiva, mostra a etapa e o percentual, permite solicitar
cancelamento, aplica timeout e impede que duas operações incompatíveis usem o
banco ao mesmo tempo. Uma falha fica isolada na própria execução e não encerra o
aplicativo.

Somente uma instância do AutoAIBuilder pode permanecer aberta por sessão do
Windows, evitando gravações concorrentes por duas janelas.

A interface fornece notificações globais acessíveis, foco visível por teclado,
contraste reforçado e atalhos `Ctrl+1` a `Ctrl+7` para as áreas principais,
`Ctrl+,` para Configurações, `F5` para atualizar a tela atual e `Esc` para fechar
a notificação. Módulos ainda não conectados às automações são identificados como
indisponíveis, sem números ou estados demonstrativos enganosos.

O shell foi reorganizado em módulos com serviços próprios de navegação,
notificações, diálogos e projeto ativo. A composição das dependências fica fora
da janela principal, e os contratos de automação não dependem do WPF. Consulte
[a documentação de arquitetura](docs/architecture.md).

A área Diagnóstico (`Ctrl+8`) apresenta runtime, sistema operacional, caminhos
de persistência, integridade do SQLite, execuções operacionais e os eventos
técnicos recentes. O log estruturado usa JSON Lines em
`%LOCALAPPDATA%\AutoAIBuilder\Logs\diagnostics.jsonl`; linhas isoladas
corrompidas são ignoradas na leitura sem impedir a inicialização. O arquivo ativo
é rotacionado ao atingir 2 MB, são preservados no máximo cinco arquivos
anteriores, eventos excessivamente grandes são reduzidos e valores com nomes de
credenciais, senhas, tokens ou chaves são removidos antes da gravação.

Leitura e edição CAD, aplicação visual de máscaras e integrações reais de IA
ainda não foram implementadas.
