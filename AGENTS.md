# AutoAIBuilder — instruções do projeto

## Premissa vigente

- Ler `docs/codex-first-plan.md` antes de ampliar o escopo.
- Preservar o aplicativo desktop; seu desenvolvimento funcional está pausado.
- Priorizar skills e agentes executados diretamente pelo Codex.
- Tratar o código atual como fonte de algoritmos, contratos e regressões.
- Não reverter nem limpar mudanças existentes no worktree.

## Segurança técnica

- Nunca alterar um DWG original; usar cópia técnica e SHA-256 antes/depois.
- Nunca executar limpeza arquitetônica automática.
- Nunca operar projeto de produção no AltoQi Builder.
- Exigir prévia, confirmação, auditoria e caminho de desfazer para ações de UI.
- Manter revisão humana para classificação, tolerância, peça, amperagem e
  aprovação do lançamento.

## Skills e agentes

- Fontes versionadas das skills: `codex/skills/`.
- Agentes personalizados do projeto: `.codex/agents/`.
- Usar `agente_mascara` para sessões delegadas de inventário e revisão CAD.
- Usar `agente_builder_eletrico` somente após máscara e calibração aprovadas.
- Não ampliar de um ponto para lote sem solicitação explícita.

## Verificação

- Validar cada skill com `quick_validate.py`.
- Testar scripts determinísticos com entradas sintéticas e artefatos reais
  somente leitura.
- Após mudanças .NET, executar:

  ```powershell
  dotnet test AutoAIBuilder.sln --configuration Debug --no-restore
  ```

