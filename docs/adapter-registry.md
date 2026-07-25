# Marco 11.6B — Registro seguro de adaptadores

O Marco 11.6B cria uma fronteira única entre contratos declarativos e código de
automação. O registro aceita somente instâncias compiladas e fornecidas
explicitamente pela composição do AutoAIBuilder.

Não existe busca em pastas, descoberta por reflexão, carregamento de DLL,
execução de script, macro ou processo externo.

## Resolução exata

Cada adaptador possui um descritor imutável com:

- identidade e versão estável do adaptador;
- fornecedor e descrição;
- identidade e versão exatas da máscara atendida;
- SHA-256 do par normalizado de contratos;
- suporte a simulação e aplicação;
- estado habilitado ou desabilitado;
- política específica para máscaras catalogadas;
- origem interna compilada.

Uma resolução somente é aprovada quando `maskId`, `maskVersion` e o SHA-256 do
pacote coincidem. A mesma identidade com conteúdo diferente é bloqueada como
contrato não homologado. Registros duplicados são rejeitados na inicialização.

O único adaptador atual continua sendo o piloto de cópia técnica verificada:

`autoaibuilder.copia-verificada@1.0.0`

Ele é interno, passa pelo novo registro e continua restrito ao fluxo
explicitamente confirmado do Marco 11.5. Sua política não autoriza a execução
de uma máscara importada pelo catálogo.

## Orquestração

`SafeAutomationOrchestrator` é a entrada única para planos internos:

1. revalida o catálogo de regras e a máscara contidos no plano;
2. normaliza novamente os dois contratos;
3. recalcula o SHA-256 e confere identidade e versão;
4. resolve o adaptador exato no registro interno;
5. confere se o modo solicitado é suportado;
6. vincula a chave de idempotência à identidade, versão e hash do adaptador;
7. somente então entrega a função do adaptador ao motor de execução isolada.

Alterar a máscara, o catálogo, o hash ou a versão do adaptador invalida a
resolução antes de qualquer criação de área de trabalho.

A entrada genérica destinada às futuras máscaras catalogadas permanece
fail-closed no 11.6B: ela pode avaliar e auditar a prontidão, mas sempre retorna
execução bloqueada.

## Avaliação na interface

O botão `Avaliar integração`:

1. exige um projeto ativo;
2. relê o snapshot persistido;
3. valida conteúdo, identidade e SHA-256;
4. exige que a versão esteja ativa;
5. consulta o registro interno;
6. aplica a política do adaptador;
7. grava a decisão no SQLite.

Essa avaliação não chama `IAutomationAdapter.ExecuteAsync`, não cria arquivos e
não prepara staging.

Estados auditáveis:

- pronta;
- máscara inativa;
- snapshot inválido;
- adaptador não registrado;
- SHA-256 não homologado;
- adaptador desabilitado;
- execução de catálogo bloqueada por política.

## Persistência

O esquema 5:

- acrescenta às auditorias de execução a identidade do catálogo de regras, o
  SHA-256 do contrato e a identidade/versão do adaptador resolvido;
- cria `AutomationIntegrationAssessments`, que preserva projeto, versão
  catalogada, hash, decisão, adaptador identificado, resumo e horário.

As migrações são aditivas. Projetos, máscaras, auditorias e arquivos existentes
não são removidos nem reescritos.

## Limites intencionais

O Marco 11.6B não:

- instala ou importa adaptadores;
- aceita caminhos de DLL, nomes de assembly ou comandos;
- autoriza execução de máscaras catalogadas;
- interpreta regras CAD;
- detecta ou controla AutoCAD, AltoQi Builder ou outro aplicativo;
- executa agentes de IA;
- altera arquivos originais.

A liberação de uma primeira máscara real exigirá um adaptador específico,
validadores próprios, testes sobre cópias e uma decisão explícita em marco
posterior.
