# Marco 11.6F — ponte com a automação AutoLISP existente

## Resultado

O AutoAIBuilder agora possui uma ponte de análise estática para as rotinas
históricas em `Automacao_CAD\04_Scripts`. A ponte cataloga os arquivos, mas não
carrega AutoLISP no AutoCAD, não chama comandos `c:`, não abre nem salva DWGs e
não cria arquivos ao lado das fontes.

## Rotinas catalogadas

| Fase | Arquivo | Responsabilidade | Tratamento no 11.6F |
|---|---|---|---|
| 01 | `mascara_analisar_v01.lsp` | inventário inicial | somente relatório |
| 02 | `mascara_classificar_v02.lsp` | classificação preliminar | somente relatório |
| 03–03.5 | `mascara_previsualizar_v03.lsp` | legenda e contexto | somente relatório |
| 04–04.2 | `mascara_previsualizar_v04.lsp` | pontos e associações | somente relatório |
| 05 | `mascara_camadas_v05.lsp` | aplicar camadas semânticas | alteração bloqueada |
| 06 | `mascara_limpeza_v06.lsp` | remover/restaurar elementos | alteração bloqueada |
| 07 | `mascara_exportar_v07.lsp` | pontos, componentes e auditoria | somente relatório |
| 08 | `mascara_builder_piloto_v08.lsp` | direção e calibração | legado substituído |
| 08.1 | `mascara_builder_piloto_v081.lsp` | direção e calibração revisada | versão preferida |

Cada arquivo recebe SHA-256, tamanho, comandos públicos encontrados, entradas,
saídas e dependências. Os hashes são calculados antes e depois da auditoria para
confirmar que as fontes permaneceram intactas.

## Contratos seguros

As rotinas foram associadas a dois contratos internos ainda não executáveis:

- `autoaibuilder.identificacao-pontos@0.1.0`: identificação elétrica e
  hidráulica, associação de componentes, exportação e direção;
- `autoaibuilder.criacao-mascara@0.1.0`: proposta de camadas e limpeza sobre
  uma futura cópia técnica.

Ambos suportam simulação. O segundo depende do primeiro e mantém a aplicação
bloqueada. Eles não são registrados como adaptadores de execução.

## Simulação

A simulação lê o `SemanticWorkspaceSnapshot` do projeto ativo e apresenta:

- total de pontos, elétricos e hidráulicos;
- componentes associados;
- camadas semânticas;
- diagnósticos de direção e pendências;
- seis ações ordenadas, distinguindo análise de ações que escreveriam durante
  uma aplicação futura.

As ações `PROPOR-CAMADAS` e `PROPOR-MASCARA` são somente descrições. Não existe
comando de aplicação na tela do marco 11.6F.

## Fronteira para os próximos marcos

- 11.6G: transformar a proposta em pré-visualização aprovável, com grupos
  originais, detectados, propostos e corrigidos;
- 11.6H: somente depois, executar a rotina aprovada sobre cópia técnica e
  comparar com a máscara histórica validada.
