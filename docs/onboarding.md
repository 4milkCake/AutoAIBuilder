# Onboarding do Pablo

## Ambiente

- Windows; Git; SDK .NET 8.0.423, conforme `global.json`.
- Visual Studio 2022 com a carga de desenvolvimento desktop .NET, se usar a IDE.
- AutoCAD 2025 somente para fluxos CAD reais; AltoQi Builder para os pilotos
  específicos. Esses programas e suas licenças não são distribuídos aqui.
- Python 3.12 para os scripts das skills. Confira os requisitos na skill utilizada.

## Primeiro uso

Aceite o convite do repositório privado e clone usando a URL fornecida no GitHub.
Na pasta clonada, execute:

```powershell
dotnet restore AutoAIBuilder.sln --locked-mode
dotnet build AutoAIBuilder.sln --configuration Debug --no-restore
dotnet test AutoAIBuilder.sln --configuration Debug --no-build --no-restore
dotnet run --project src/AutoAIBuilder.Desktop --no-build
```

Também é possível abrir `AutoAIBuilder.sln` no Visual Studio.
O aplicativo cria seus dados em `%LOCALAPPDATA%\AutoAIBuilder`.
Referências a arquivos locais de Danilo na documentação são exemplos históricos;
configure seus próprios caminhos. Os datasets reais não acompanham o clone.

Leia `AGENTS.md`, `PROJECT_HANDOFF.md`, `docs/codex-first-plan.md` e
`docs/roadmap-current.md` antes de alterar funcionalidades. O desktop está
preservado e em pausa funcional enquanto as skills são homologadas.

## Colaboração

Crie uma branch por tarefa e envie pull request para revisão de Danilo.
Não envie credenciais, dados de clientes, DWGs originais, bancos locais ou
arquivos de compilação. Exemplos de configuração devem usar placeholders.
As rotinas históricas v05/v06 alteram entidades e permanecem bloqueadas.
Pilotos CAD/Builder exigem cópia técnica, revisão humana e trilha de auditoria.

Em conta pessoal, o colaborador recebe acesso de escrita; em organização,
prefira Write. Maintain só é necessário se Pablo também administrar o repositório.
