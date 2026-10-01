# Artefatos do AutoAIBuilder

## Repositório

Raiz padrão:

```text
D:\Projetos\AutoAIBuilder
```

Componentes reaproveitados:

- `src/AutoAIBuilder.Infrastructure/Recognition/AutoCadRecognitionInventory.lsp`
- `src/AutoAIBuilder.Infrastructure/Recognition/CadEntityInventoryParser.cs`
- `src/AutoAIBuilder.Infrastructure/Recognition/CadExpandedGeometryAnchorResolver.cs`
- `src/AutoAIBuilder.Infrastructure/Recognition/CadLegendInterpreter.cs`
- `src/AutoAIBuilder.Infrastructure/Recognition/CadRecognitionService.cs`
- `tests/AutoAIBuilder.Tests/CadRecognitionServiceTests.cs`
- `docs/recognition-11.6i.md`
- `docs/codex-first-plan.md`

## Acervo histórico

Raiz padrão:

```text
C:\Users\danil\OneDrive\Documentos\Automacao_CAD
```

Fontes principais:

- `02_Trabalho/TESTE_01_ARQUITETURA_ORIGINAL.dwg`
- `02_Trabalho/TESTE_01_MASCARA_AUTOMATICA_V06.dwg`
- `02_Trabalho/TESTE_01_MASCARA_AUTOMATICA_V06_pontos_semanticos_v07.csv`
- `02_Trabalho/TESTE_01_MASCARA_AUTOMATICA_V06_componentes_semanticos_v07.csv`
- `02_Trabalho/TESTE_01_MASCARA_AUTOMATICA_V06_diagnostico_direcoes_v081.csv`
- `04_Scripts/mascara_previsualizar_v03.lsp`
- `04_Scripts/mascara_previsualizar_v04.lsp`
- `04_Scripts/mascara_exportar_v07.lsp`
- `04_Scripts/mascara_builder_piloto_v081.lsp`

Linha de base: 272 pontos, 196 elétricos, 76 hidráulicos, 100 componentes, 35
layers semânticas e 38 diagnósticos de direção.

## Sessões atuais

Raiz padrão:

```text
%LOCALAPPDATA%\AutoAIBuilder\Data\Recognition
```

Localizar a pasta mais recente que contenha `session.json`; não assumir que o
ID documentado ainda é o mais atual.

Última linha de base observada em 30/07/2026:

- arquivo `Elétrico - Maíra e Pedro.dwg`;
- 9.421 registros AAR v3;
- 581 inserções;
- 513 candidatos;
- 161 relacionados à legenda;
- 28 pares de legenda;
- 12 descrições sem par;
- 243 âncoras `INSERTION_WCS`;
- 114 âncoras `BOUNDS_CENTER_WCS`;
- 156 âncoras `EXPANDED_GEOMETRY_CENTER_WCS`;
- zero candidatos revisados.

## Regressões luminotécnicas aprovadas

- térreo:
  `artifacts/codex-first/recognition/luminotecnico-terreo/20260731-125222-8a9d3fa654be48e79793b18df74dbe9e`;
- segundo pavimento:
  `artifacts/codex-first/recognition/luminotecnico-2-pavimento/20260802-141659-9809a0c525454bef9166f801c8998e40`.

Ambos têm manifesto `APPROVED_BY_USER`, coordenadas WCS, decisões e evidências.
Ler `luminotecnico-regression-cases.md` para os oráculos específicos; não
generalizar contagens ou ambientes para outro desenho.
