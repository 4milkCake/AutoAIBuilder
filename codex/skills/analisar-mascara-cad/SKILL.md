---
name: analisar-mascara-cad
description: Inventariar, reconhecer e revisar símbolos e pontos elétricos, hidráulicos ou de iluminação em desenhos DWG e em sessões de reconhecimento do AutoAIBuilder. Usar para analisar máscaras CAD, interpretar legendas, distinguir exemplares da legenda de ocorrências da planta, conferir UCS/WCS, escala e orientação visual em relação às paredes, localizar falsos positivos ou ausências e produzir um manifesto semântico auditável sem alterar o DWG original.
---

# Analisar máscara CAD

Executar reconhecimento conservador e supervisionado. Tratar a integridade do
DWG, a evidência geométrica e a revisão profissional como portões obrigatórios.

## Escolher o modo

1. Se houver `session.json`, usar **revisão de sessão existente**.
2. Se houver somente um DWG, usar **inventário de novo DWG**.
3. Se faltarem o DWG e uma sessão, solicitar um deles.

## Revisar uma sessão existente

1. Localizar `session.json` e o DWG de origem.
2. Executar:

   ```powershell
   python scripts/inspect_recognition_session.py <session.json> --source-dwg <origem.dwg>
   ```

3. Interromper se o SHA-256 divergir.
4. Gerar as filas:

   ```powershell
   python scripts/export_review_queues.py <session.json> --output-dir <pasta>
   ```

5. Ler [domain-rules.md](references/domain-rules.md).
6. Revisar primeiro os pares da legenda e registrar os não pareados.
7. Separar explicitamente `LEGEND_EXEMPLAR` de `PLANT_OCCURRENCE`; nunca
   apresentar a coordenada do exemplar da legenda como coordenada do ponto.
8. Revisar amostras de `INSERTION_WCS`, `BOUNDS_CENTER_WCS` e
   `EXPANDED_GEOMETRY_CENTER_WCS`.
9. Apresentar candidatos em lotes pequenos, sempre com handle, descrição,
   papel da entidade, origem da âncora, X/Y/Z, sistema de coordenadas, rotação
   bruta, orientação visual efetiva, confiança e evidência.
10. Registrar cada decisão como `APPROVED`, `REJECTED`, `CORRECTED` ou
   `PENDING`; não transformar ausência de revisão em aprovação.
11. Produzir as saídas definidas em
   [output-contracts.md](references/output-contracts.md).

## Inventariar um novo DWG

1. Exigir uma pasta de saída separada e gravável.
2. Confirmar que o AutoCAD desktop está fechado. Não encerrá-lo
   automaticamente; o perfil `acad2025.cfg` pode estar bloqueado.
3. Executar:

   ```powershell
   python scripts/export_cad_inventory.py <origem.dwg> --output-root <pasta>
   ```

4. Confirmar no manifesto:
   - hash original antes e depois;
   - cópia técnica criada;
   - `AAR|3`;
   - zero gravações no original;
   - log do AutoCAD Core Console disponível.
5. Tratar o AAR como inventário, não como classificação aprovada.
6. Executar a varredura de símbolos compostos por linhas quando a legenda ou a
   revisão visual indicar que pontos podem não ser blocos:

   ```powershell
   python scripts/scan_loose_outlet_triangles.py <inventario.aar> `
     --output <pasta>/loose-outlet-candidates.json `
     --include-bounds <min-x> <min-y> <max-x> <max-y>
   ```

   O limite deve abranger somente a planta a revisar, excluindo a legenda. O
   resultado é uma fila `PENDING`: conferir cor, legenda, ambiente e orientação
   visual antes de incorporá-lo ao registro de coordenadas.
7. Se ainda não houver `recognition-candidates.json`, declarar que a etapa
   semântica Codex-first permanece pendente. Não inventar candidatos.

O executor usa o AutoLISP AAR v3 versionado no projeto e abre somente a cópia
técnica. Ele pode expandir blocos nessa cópia, encerra sem salvar e revalida o
original.

## Organizar a revisão visual

1. Em semântica nova, revisar primeiro a legenda e amostras pequenas.
2. Depois de estabilizar o catálogo, preferir revisão por ambiente com uma
   captura geral e recortes legíveis. Informar tipos, quantidades e comandos;
   manter o registro ponto a ponto em JSON/CSV.
3. Se o usuário solicitar execução completa sem aprovações intermediárias,
   concluir inventário, classificação e evidências, mas manter decisões
   `PENDING` até a manifestação final.
4. Não usar código de comando como tipo nem como nome de ambiente. Delimitar o
   ambiente pela arquitetura completa e pela continuidade espacial.
5. Registrar correção de ambiente separadamente da correção de tipo ou
   orientação; uma não implica automaticamente as outras.

## Revisar coordenada, escala e orientação

Ler [orientation-and-coordinate-review.md](references/orientation-and-coordinate-review.md)
antes de comparar o inventário com o painel Propriedades ou inferir direção.

1. Identificar se o registro é exemplar da legenda ou ocorrência real.
2. Tratar o AAR como WCS. Tratar a coordenada mostrada nas Propriedades como
   potencial UCS atual até confirmar o sistema ativo.
3. Confirmar qualquer conversão UCS→WCS com pelo menos duas ocorrências
   correspondentes; registrar transformação e resíduos, sem generalizar para
   outro arquivo ou depois de mudança de UCS.
4. Separar escala informada, fator de unidade e escala efetiva.
5. Preservar `raw_rotation_deg`; não convertê-la diretamente em direção.
6. Determinar `effective_orientation` pela geometria transformada do símbolo,
   incluindo espelhamento, eixo nativo, hierarquia, parede próxima e lado do
   ambiente. Marcar `NOT_APPLICABLE` quando o tipo não exigir direção.
7. Manter a orientação `PENDING_VISUAL_REVIEW` quando geometria e contexto não
   forem conclusivos.

## Fechar um pacote aprovado

Após a confirmação explícita do usuário, atualizar decisões e manifesto e
executar:

```powershell
python scripts/validate_mask_package.py <pasta-da-sessao> --require-approved
```

Adicionar `--source-dwg <origem.dwg>` para revalidar o SHA-256 quando a origem
estiver disponível. Não emitir `APPROVED_BY_USER` se o validador apontar erro.

## Portões

- Nunca editar, salvar, limpar, explodir ou apagar no DWG original.
- Nunca contar exemplares da legenda como pontos da planta.
- Nunca apresentar coordenada de legenda sem o rótulo `LEGEND_EXEMPLAR`.
- Nunca promover associação de legenda a alta confiança sem revisão.
- Preservar ponto de inserção original e âncora WCS resolvida.
- Não confundir WCS com UCS atual, coordenada de tela ou coordenada do Builder.
- Não tratar rotação bruta do bloco como orientação visual efetiva.
- Manter símbolos não classificados visíveis.
- Não concluir que um ponto está ausente somente porque não existe como
  `INSERT`; executar a varredura de geometria solta e revisar seus candidatos.
- Parar quando o executável oficial do AutoCAD não puder ser confirmado.
- Parar quando o AutoCAD desktop estiver aberto; nunca encerrá-lo
  automaticamente.

## Referências

- Ler [project-artifacts.md](references/project-artifacts.md) ao trabalhar no
  AutoAIBuilder atual.
- Ler [domain-rules.md](references/domain-rules.md) para classificação,
  legenda, texto, hierarquia e preservação.
- Ler [orientation-and-coordinate-review.md](references/orientation-and-coordinate-review.md)
  para revisar UCS/WCS, escala e direção visual.
- Ler [output-contracts.md](references/output-contracts.md) antes de gravar
  decisões ou manifestos.
- Ler [luminotecnico-regression-cases.md](references/luminotecnico-regression-cases.md)
  ao analisar iluminação ou validar regressões; usar os números somente como
  oráculos das sessões documentadas.
- Ler [pontos-terreo-regression-cases.md](references/pontos-terreo-regression-cases.md)
  ao analisar máscaras elétricas, hidráulicas e sanitárias combinadas; nunca
  transportar coordenadas ou quantidades para outro hash.
