# Preparação para publicação privada

Data: 01/10/2026.

## Fontes preservadas

- Código e documentação de `D:\Projetos\AutoAIBuilder`, incluindo as alterações
  ainda sem commit. A pasta original permanece intacta.
- Os oito commits existentes foram importados para a cópia de publicação.
- Handoff Markdown e PDF recuperados da pasta local do projeto.
- Nove rotinas de `Automacao_CAD/04_Scripts`, sem alterações.
- Nove variantes adicionais de fontes AutoLISP encontradas nas execuções
  históricas, deduplicadas por SHA-256. A proveniência está em
  `legacy-adapters-provenance.json`; elas são referências históricas e não devem
  ser carregadas automaticamente.
- Fontes das skills e agentes, schemas e arquivos de dependências travadas.

## Exclusões

Compilação, caches, temporários de validação/upload, logs, bancos locais,
credenciais e pacotes ficam ignorados. `artifacts/` e os desenhos CAD reais
não foram copiados para publicação. Assim, dados e evidências de clientes
não entram automaticamente no GitHub. A imagem de referência visual e a
documentação PDF foram preservadas. O exemplo de calibração CSV é sintético.

Uma busca por padrões de credenciais nos arquivos candidatos não encontrou
segredos. Os alertas de `sk-...` eram nomes como `mask-recognition-manifest.json`.
Também não foram encontrados padrões de tokens conhecidos/chaves privadas
nos oito commits históricos. A inspeção por padrões não constitui garantia
contra todo tipo de dado sensível. Não há arquivos acima de 10 MB entre
os candidatos nem entre os blobs históricos; não foi necessário Git LFS.

## Verificação

- SDK .NET 8.0.423, restauração com dependências travadas usando o cache local.
- Compilação Debug: zero erros e zero avisos.
- Testes: 148 aprovados, zero falhas e zero ignorados.
- O acesso ao serviço de vulnerabilidades NuGet estava indisponível;
  a restauração final local usou `NuGetAudit=false`. O workflow CI original
  continua com a configuração normal, sem essa alteração.
- Nenhuma integração com AutoCAD/Builder foi executada nesta preparação.

## Etapa externa pendente

O repositório privado `https://github.com/4milkCake/AutoAIBuilder` foi criado,
e o remote `origin` foi configurado. O push depende da autenticação do Git no
Windows; o convite de Pablo depende da identificação de sua conta.
Este documento registra a preparação e não confirma o estado remoto do código.
