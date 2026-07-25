# Contratos seguros de automação

O Marco 11.4 define a fronteira de segurança usada antes da importação das
máscaras reais. Ele não contém integração CAD, não importa fluxos e não habilita
execuções na interface.

## Versões dos contratos

- catálogo de regras: esquema `1.0`;
- máscara: esquema `1.0`;
- versões de catálogo, regra, máscara, dependência e aplicativo: `X.Y.Z`,
  sempre estáveis e sem sufixos Preview ou RC;
- identificadores: letras minúsculas, números, pontos e hífens.

Os documentos JSON devem ser validados pelos esquemas
[`automation-rule-catalog.schema.json`](../schemas/automation-rule-catalog.schema.json)
e [`automation-mask.schema.json`](../schemas/automation-mask.schema.json). Um
esquema desconhecido é rejeitado; não há conversão silenciosa.

O catálogo informa a origem, severidade, condição e extensões de cada regra. A
máscara declara compatibilidade, dependências, parâmetros, saídas,
pré-condições, pós-condições, suporte a simulação e idempotência. Uma máscara que
referencie regra ausente ou desabilitada é rejeitada.

## Ciclo obrigatório

1. validar os contratos, as dependências e os parâmetros;
2. localizar cada entrada e calcular seu SHA-256;
3. produzir um plano legível, com uma chave de idempotência determinística;
4. simular e apresentar o plano sem criar a área de saída;
5. após confirmação futura, conferir novamente os checksums;
6. criar uma área exclusiva em `<saída>\.autoaibuilder\staging`;
7. copiar as entradas e conferir os checksums das cópias;
8. entregar ao adaptador somente os caminhos das cópias e da pasta de resultado;
9. validar as saídas e conferir novamente os originais;
10. publicar a pasta exclusiva ou mover artefatos parciais para
    `<saída>\.autoaibuilder\recovery`;
11. persistir as evidências no SQLite.

A simulação registra a auditoria, mas não cria pastas, cópias nem arquivos na
raiz de saída. A função de automação não é chamada durante a simulação.

## Garantias de segurança

- O adaptador não recebe caminhos originais; recebe somente as cópias.
- Nenhum arquivo existente é sobrescrito: cópias usam criação exclusiva e cada
  execução possui um identificador próprio.
- SHA-256 é calculado no planejamento, antes e depois da cópia e antes da
  publicação.
- Uma alteração externa no original bloqueia a execução; o aplicativo não tenta
  desfazê-la.
- Uma falha nunca apaga artefatos parciais. A área é movida para recuperação e
  fica disponível para inspeção manual.
- Caminhos declarados fora da área isolada ou com travessia `..` são rejeitados.
- Uma execução concluída com a mesma máscara, versão, catálogo, entradas,
  checksums, parâmetros, projeto e raiz de saída é reutilizada sem invocar de
  novo o adaptador.
- Pré e pós-validadores adicionais podem bloquear uma máscara específica sem
  depender da interface WPF.

## Auditoria e recuperação

O esquema 3 do banco inclui `AutomationAudits`. Cada registro mantém projeto,
plano, máscara e versão, modo, estado, chave de idempotência, snapshots das
entradas, caminhos de saída, pasta publicada ou de recuperação, resumo e
horários.

Estados possíveis:

- `Simulated`: plano aprovado sem execução;
- `Running`: trabalho isolado em andamento;
- `Succeeded`: saída publicada e validada;
- `Reused`: resultado idempotente já existente;
- `Rejected`: pré-validação bloqueou o plano;
- `FailedRolledBack`: falha contida, com artefatos preservados quando existiam;
- `CancelledRolledBack`: cancelamento cooperativo, também preservado;
- `Interrupted`: processo anterior terminou antes de registrar um estado final.

Na inicialização, auditorias incompletas são marcadas como interrompidas. O
aplicativo não apaga automaticamente pastas de staging ou recuperação.

## Marco 11.5

O primeiro adaptador de baixo risco está implementado:

`selecionar → validar → simular → mostrar plano → confirmar → executar sobre
cópia → validar saída → registrar resultado`.

Ele produz uma cópia byte a byte e um manifesto, sem interpretar o arquivo. A
implementação e os limites estão documentados em
[Cópia técnica verificada](verified-copy-pilot.md). Nenhuma máscara gráfica real
é considerada instalada.

## Marco 11.6A

O módulo `Máscaras` recebe um contrato de máscara e um catálogo de regras,
executa a validação completa e apresenta uma pré-visualização antes de qualquer
gravação. Pacotes aprovados são persistidos inativos no esquema 4 do SQLite.

A identidade `maskId@maskVersion` é imutável: conteúdo diferente exige uma nova
versão. Conteúdo idêntico não é duplicado. Uma restrição do banco permite apenas
uma versão ativa de cada máscara, mas esse estado representa somente a escolha
para integração futura.

O catálogo não carrega adaptadores, scripts, DLLs, macros ou executáveis. Os
detalhes estão em [Catálogo seguro de máscaras](mask-catalog.md).
