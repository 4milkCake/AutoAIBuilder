# AutoAIBuilder

## Onboarding e colaboração

Comece pelo [PROJECT_HANDOFF.md](PROJECT_HANDOFF.md), preservado do handoff de
01/10/2026. Para a direção vigente, consulte [Plano Codex-first](docs/codex-first-plan.md)
e [roadmap atual](docs/roadmap-current.md): o handoff contém também decisões históricas.
O [guia do Pablo](docs/onboarding.md) reúne instalação, compilação, execução e colaboração.
O [índice da documentação](docs/README.md) orienta a leitura por assunto.

As rotinas AutoLISP históricas estão em `prototypes/autocad/legacy`.
Os DWGs reais, bancos locais e evidências de clientes não fazem parte do repositório.
Consulte [preparação para publicação](docs/repository-publication.md).

Aplicação desktop WPF para coordenar automações e agentes de IA aplicados a
projetos elétricos e hidrossanitários.

> **Direção vigente:** o aplicativo está preservado e em pausa funcional
> controlada enquanto máscara, coordenadas e o primeiro lançamento elétrico
> são validados diretamente no Codex. Consulte o
> [Plano Codex-first](docs/codex-first-plan.md).

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

O banco usa atualmente o esquema 5. Além dos dados funcionais, ele registra o
estado das execuções operacionais, incluindo progresso, timeout, cancelamento,
falha e conclusão. Execuções que estavam pendentes ou em andamento quando o
processo foi encerrado são recuperadas como interrompidas no próximo início.

O Marco 11.4 acrescenta contratos versionados para catálogos de regras e
máscaras, pré e pós-validação, planejamento, simulação, execução exclusivamente
sobre cópias, checksums SHA-256, recuperação preservada, auditoria e
idempotência. Os contratos JSON estão em `schemas` e o ciclo completo está em
[Contratos seguros de automação](docs/automation-contracts.md). Nenhuma máscara
real foi importada ou habilitada nessa etapa.

O Marco 11.5 habilita apenas o módulo `Automação` com o piloto
`Cópia técnica verificada`. O usuário seleciona uma entrada catalogada e uma
pasta de saída, executa uma simulação sem arquivos, confere o plano e confirma
explicitamente a aplicação. O resultado contém uma cópia byte a byte e um
manifesto JSON; o original é comprovado por SHA-256 antes e depois. Consulte
[Marco 11.5 — Cópia técnica verificada](docs/verified-copy-pilot.md).

O Marco 11.6A habilita o módulo `Máscaras` como um catálogo declarativo seguro.
Um contrato de máscara e seu catálogo exato de regras são analisados,
normalizados e identificados por SHA-256 antes de serem armazenados inativos.
Conflitos de conteúdo nunca sobrescrevem a mesma versão, e somente uma versão
por identificador pode ser marcada para integração futura. Importar ou ativar
não carrega código nem executa automações. Consulte
[Marco 11.6A — Catálogo seguro de máscaras](docs/mask-catalog.md).

O Marco 11.6B introduz um registro interno e imutável de adaptadores. A
resolução exige identidade, versão e SHA-256 exatos; não existe descoberta de
DLLs, scripts ou executáveis. O piloto de cópia verificada passa pelo novo
orquestrador, e sua versão fica registrada nas auditorias. Máscaras catalogadas
podem ser revalidadas e ter a prontidão auditada, mas a execução continua
bloqueada. Consulte
[Marco 11.6B — Registro seguro de adaptadores](docs/adapter-registry.md).

O Marco 11.6C conecta os relatórios reais v07/v081 produzidos pela automação
histórica ao modelo interno do AutoAIBuilder. O módulo `Análise semântica`
valida e importa pontos, componentes e diagnósticos para o SQLite, calcula
SHA-256, reproduz a linha de base auditada de 272 pontos e oferece pesquisa,
filtros, prévia espacial e estados de revisão humana. A origem permanece
somente leitura e nenhuma ação CAD é executada. Consulte a
[ponte semântica e revisão inicial](docs/semantic-bridge.md), a
[avaliação de reaproveitamento](docs/automation-reuse-assessment.md) e o
[pacote de recepção](docs/automation-source-intake.md).

O Marco 11.6D transforma a análise em uma central de revisão supervisionada.
Ela explica as evidências de cada ponto, mostra componentes e textos
associados, reúne pendências, permite corrigir pontos e direções, mantém
histórico reversível e cria um dicionário restrito ao projeto. A aplicação de
uma correção a itens semelhantes exige confirmação e gera auditoria individual.
Consulte a
[Central avançada de revisão semântica](docs/semantic-review-advanced.md).

O ajuste 11.6D.1 conecta a fila de pendências à localização visual: **Revisar**
abre a Visão geral, seleciona e rola a tabela até o item, identifica o ponto no
topo do mapa e aplica um marcador destacado. Os pontos do mapa também são
clicáveis. Para a próxima geração do visualizador, foi adotado o AutoCAD 2025
como primeiro motor de leitura/conversão, mantendo um contrato substituível para
um futuro leitor DWG incorporado. Consulte a
[decisão de visualização CAD](docs/cad-visualization-decision.md).

O Marco 11.6E.1 entrega a visualização arquitetônica real em uma janela
maximizada, com seleção centralizada, filtros de layers e indicador de
cobertura. O AutoCAD
Core Console lê exclusivamente uma cópia técnica verificada do DWG e exporta
um artefato vetorial interno. A central semântica desenha a planta sob os
pontos já importados, com zoom, movimentação, ajuste à tela, filtros de layers
e seleção sincronizada com o editor detalhado. O cache é identificado pelo
SHA-256 do DWG; se o original mudar, o artefato anterior não é reutilizado.
Consulte a
[Visualização arquitetônica supervisionada](docs/cad-visualization.md).

O Marco 11.6F cria a primeira ponte com as nove rotinas AutoLISP históricas.
Ela extrai comandos, entradas, saídas e dependências por análise estática,
confirma os arquivos por SHA-256 e associa a cadeia a contratos internos de
identificação de pontos e criação de máscara. As versões 05 e 06, que contêm
operações capazes de modificar entidades, ficam bloqueadas. A tela simula a
sequência sobre a base semântica já importada, sem carregar AutoLISP, executar
comandos CAD ou alterar DWGs. Consulte a
[ponte com a automação existente](docs/automation-bridge-11.6f.md).

O Marco 11.6G acrescenta a pré-visualização supervisionada antes/depois. A
arquitetura original permanece protegida e os elementos detectados, propostos,
corrigidos ou temporariamente ignorados aparecem em grupos independentes. Cada
grupo pode ser aprovado ou rejeitado com observação auditável; as decisões são
vinculadas ao SHA-256 do plano. Mesmo com todos os grupos aprovados, a tela não
executa AutoLISP nem grava o DWG. Consulte a
[pré-visualização supervisionada](docs/automation-preview-11.6g.md).

O Marco 11.6H comprovou a execução isolada no AutoCAD com 272/272 pontos e
100/100 componentes corretos. A inspeção visual posterior reprovou a limpeza
arquitetônica porque uma referência de bloco composta foi removida. A premissa
vigente passa a ser preservação total da arquitetura e foco em identificação,
coordenadas, altura e orientação dos pontos.

O **11.6H.1 — Modo de preservação total** corrigiu essa fronteira e foi
aprovado em execução real: 272/272 pontos, 100/100 componentes e 1.400/1.400
entidades preservadas, sem handles ausentes. O próximo ciclo é o reconhecimento
supervisionado de um DWG novo. Consulte a
[rota atual do produto](docs/roadmap-current.md) e a
[execução supervisionada](docs/automation-supervised-11.6h.md).

O Marco 11.6I acrescenta o reconhecimento supervisionado de um novo DWG. O
AutoCAD inventaria uma cópia técnica sem mutações, o sistema compara blocos com
a base validada, atribui confiança e permite aprovar, rejeitar ou corrigir cada
candidato sobre a planta. Consulte o
[reconhecimento 11.6I](docs/recognition-11.6i.md).

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

Leitura e edição CAD, aplicação visual das máscaras definitivas, adaptadores CAD
e integrações reais de IA ainda não foram implementadas. O piloto 11.5 não
interpreta o conteúdo técnico dos arquivos; o catálogo 11.6A armazena contratos
declarativos, e o registro 11.6B somente comprova a ligação com componentes
internos homologados.
