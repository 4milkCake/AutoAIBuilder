# Marco 11.5 — Cópia técnica verificada

O primeiro piloto executável do AutoAIBuilder comprova o ciclo completo das
automações sem interpretar ou editar conteúdo CAD. Ele aceita um arquivo já
catalogado, cria uma cópia byte a byte e publica um manifesto JSON com as
evidências da execução.

Este piloto não é uma das máscaras definitivas do produto.

## Fluxo na interface

1. Abrir `Automação` (`Ctrl+9`).
2. Selecionar um arquivo acessível do projeto ativo.
3. Escolher explicitamente uma pasta de saída.
4. Clicar em `Validar e simular`.
5. Conferir SHA-256, chave de idempotência, etapas e eventuais bloqueios.
6. Clicar em `Confirmar e executar sobre cópia`.
7. Confirmar o arquivo, o destino e o checksum no diálogo nativo.
8. Aguardar pré-validação, cópia, pós-validação, publicação e auditoria.

A simulação grava somente sua auditoria no banco local. Ela não cria a pasta de
saída, não copia a entrada e não chama o adaptador.

## Contrato do piloto

- máscara: `copia-tecnica-verificada@1.0.0`;
- catálogo: `regras-piloto-seguro@1.0.0`;
- entrada: exatamente um arquivo catalogado e acessível;
- extensões: DWG, DXF, IFC, PDF, DOC, DOCX, XLS, XLSX, CSV e imagens
  catalogáveis;
- saídas obrigatórias:
  - `result/verified-files/<nome-original>`;
  - `result/manifest.json`.

Antes da simulação, os validadores do projeto também são executados. Se as
regras do projeto determinarem bloqueio diante de erros, o plano fica rejeitado
e nenhuma saída é criada.

## Manifesto

O `manifest.json` contém:

- esquema do manifesto;
- identidade e versão do piloto;
- identidade da execução;
- nome seguro do arquivo;
- SHA-256 da entrada isolada;
- caminho relativo da cópia;
- SHA-256 da cópia verificada;
- horário UTC.

A pós-validação relê o manifesto e os dois arquivos. Qualquer diferença de
identidade, caminho ou checksum impede a publicação.

## Segurança e recuperação

- O adaptador recebe somente a cópia criada pela camada segura.
- O original é conferido novamente antes e depois do adaptador.
- Cópia e manifesto usam caminhos internos controlados.
- Nenhum arquivo existente é sobrescrito.
- Falhas e cancelamentos preservam a área parcial em
  `.autoaibuilder/recovery`.
- A confirmação ocorre depois da simulação e antes de qualquer gravação na
  pasta escolhida.
- Simulação e aplicação passam pelo motor operacional com timeout, cancelamento
  e exclusão por projeto.

Se a mesma execução já tiver sido concluída e todas as evidências publicadas
continuarem presentes, o resultado é reutilizado. Se a pasta ou algum arquivo
auditado não existir mais, o piloto não reutiliza a auditoria antiga e gera uma
nova saída.

## Limites intencionais

O piloto não:

- abre, interpreta ou altera DWG, DXF ou IFC;
- aplica uma máscara gráfica;
- inicia AutoCAD ou AltoQi Builder;
- chama agentes de IA;
- grava dentro do projeto original;
- remove automaticamente staging, recuperação ou resultados.

Essas integrações permanecem fora do escopo até a importação controlada das
máscaras reais.
