# Marco 11.6D — Central avançada de revisão semântica

## Objetivo

O Marco 11.6D transforma a consulta criada no 11.6C em uma bancada de revisão
supervisionada. O usuário pode compreender por que um ponto foi classificado,
corrigir seus dados, revisar direções, localizar candidatos semelhantes e
desfazer alterações. Todas as decisões ficam no banco local; o DWG e os CSVs
permanecem intactos.

## Áreas da interface

O módulo **Análise semântica** contém quatro áreas:

1. **Visão geral** — mapa por coordenadas, pesquisa, filtros, tabela de pontos,
   editor, componentes e candidatos semelhantes.
2. **Pendências** — reúne pontos marcados para revisão, alturas
   `A_CONFIRMAR`, classificações abaixo de confiança alta e direções duvidosas.
3. **Direções** — mostra os 38 diagnósticos v081 e permite aprovar ou corrigir
   `DIREITA`, `ESQUERDA`, `CIMA` e `BAIXO`.
4. **Histórico e aprendizado** — apresenta revisões ativas ou desfeitas e o
   dicionário restrito ao projeto atual.

## Explicação e associações

Ao selecionar um ponto, a central apresenta:

- fonte da classificação;
- confiança;
- camada semântica;
- nome do bloco;
- quantidade de componentes gráficos;
- textos associados;
- conteúdo, tipo, classe e distância dos componentes vinculados.

Essa explicação é determinística e usa os dados dos CSVs. Ela não inventa uma
justificativa por IA.

## Correções e histórico

O editor permite alterar:

- disciplina;
- código semântico;
- descrição;
- altura numérica ou condição textual;
- observação profissional.

Cada alteração grava uma revisão com:

- item afetado;
- ação;
- resumo da mudança;
- observação;
- estado anterior e posterior;
- data e hora;
- estado ativo ou desfeito.

O comando **Desfazer** restaura o estado anterior sem apagar a auditoria. Uma
revisão desfeita continua visível no histórico.

## Semelhança supervisionada

A busca de semelhantes é deliberadamente conservadora. Ela compara:

- bloco;
- camada semântica;
- código semântico quando aplicável;
- proximidade apenas para ordenar os resultados.

O AutoAIBuilder apenas apresenta candidatos. A aplicação em lote exige uma
confirmação explícita e grava uma revisão individual para cada ponto afetado.
Não há classificação silenciosa.

## Conhecimento do projeto

Uma correção pode gerar uma entrada no dicionário do projeto com:

- assinatura formada pelo bloco e pela camada;
- código anterior;
- código aprendido;
- descrição;
- altura;
- quantidade de evidências;
- origem e data.

Esse conhecimento não é promovido automaticamente para outros projetos ou
arquitetos. Também não treina um modelo externo. Ele é uma memória local,
versionada e auditável para as próximas fases.

## Persistência

O esquema SQLite 7 acrescenta:

- campos de revisão em `SemanticDirectionDiagnostics`;
- `SemanticReviewRevisions`;
- `SemanticKnowledgeEntries`.

Correções ativas, direções revisadas, histórico e conhecimento são preservados
quando o mesmo conjunto v07/v081 é reimportado.

## Segurança

O 11.6D não:

- abre ou controla o AutoCAD;
- executa AutoLISP;
- escreve no DWG;
- altera os CSVs;
- gera máscara;
- controla o AltoQi Builder;
- envia dados a uma API;
- aplica regras aprendidas sem confirmação.

## Evolução concluída no 11.6E

O Marco 11.6E acrescentou a planta arquitetônica real como fundo da revisão.
O AutoCAD Core Console processa somente uma cópia técnica do DWG, e o
AutoAIBuilder consome um artefato vetorial validado. Zoom, movimentação,
controle de layers e seleção sincronizada passam a ocorrer dentro da própria
central. A nova etapa continua sem editar o DWG, aplicar máscaras ou enviar
dados ao Builder. Consulte
[Visualização arquitetônica supervisionada](cad-visualization.md).

## Critérios automatizados

Os testes cobrem:

- correção de ponto;
- persistência da correção após reimportação;
- criação e remoção coerente de conhecimento ao desfazer;
- localização determinística de semelhantes;
- aplicação confirmável em lote com histórico individual;
- revisão e desfazer de direção;
- regressão dos 272 pontos históricos.
