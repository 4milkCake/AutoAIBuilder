# Marcos 11.6H e 11.6H.1 — Execução supervisionada

## Objetivo vigente

Organizar os pontos e componentes semânticos aprovados somente sobre uma cópia
técnica, sem limpar ou excluir qualquer conteúdo arquitetônico. O original e a
referência histórica são protegidos por SHA-256.

O perfil vigente é:

```text
PRESERVATION_TOTAL_11.6H.1
```

## Mudança em relação ao 11.6H

A primeira execução 11.6H aplicava nove regras históricas de limpeza. Ela
obteve 272/272 pontos e 100/100 componentes corretos, mas removeu uma referência
de bloco `CINZA PONTOS` que continha portas, janelas, mobiliário, bancadas,
cubas e eletrodomésticos. A identificação semântica foi aprovada; a máscara
visual foi reprovada.

O 11.6H.1 elimina essa fronteira de risco:

- não lê o catálogo de classificação v02;
- não recebe regras `REMOVER`;
- não gera nem carrega código de exclusão;
- não apaga entidades, blocos, layers ou referências;
- altera somente o código DXF de layer dos handles semânticos aprovados;
- preserva as rotinas históricas originais apenas como evidência, sem
  carregá-las no AutoCAD.

O adaptador gerado possui uma verificação defensiva que bloqueia a execução se
o texto `entdel` estiver presente.

## Prova de preservação

Antes de organizar os pontos, o adaptador registra todos os handles do Model
Space e conta as referências do bloco `CINZA PONTOS`. Depois da organização,
repete a leitura e grava:

- quantidade de entidades antes;
- quantidade de entidades depois;
- handles ausentes;
- quantidade de referências `CINZA PONTOS` antes;
- quantidade de referências `CINZA PONTOS` depois.

A execução só pode ser aprovada quando:

- entidades antes e depois são iguais;
- nenhum handle está ausente;
- a quantidade do bloco protegido não mudou;
- todos os pontos estão na layer esperada;
- todos os componentes estão na layer esperada;
- a comparação semântica com a V06 passa;
- original e referência histórica mantêm os hashes.

## Portões obrigatórios

- plano 11.6G integralmente aprovado;
- DWG original disponível;
- máscara histórica disponível e diferente do original;
- relatório de pontos v07 compatível;
- catálogo histórico da legenda disponível;
- rotinas históricas presentes para auditoria;
- AutoCAD Core Console oficial disponível;
- pasta de saída isolada;
- base semântica revisada;
- perfil de preservação total ativo.

O catálogo de classificação v02 não é mais um insumo nem um portão.

## Estrutura da execução

```text
<destino>/<project-id>/<data-hora>-<run-id>/
├── entrada/
│   └── ORIGINAL_VERIFICADO.dwg
├── resultado/
│   └── AUTOAIBUILDER_PONTOS_PRESERVADOS_<data-hora>.dwg
├── rotinas-originais/
├── adaptadores/
│   └── autoaibuilder-preservacao-11.6h1.lsp
├── resultado-entidades.csv
├── resultado-preservacao.csv
├── autoCAD-core-console.log
└── manifesto-11.6h.json
```

Cada execução recebe pasta própria. O resultado nunca substitui o original,
a referência histórica ou uma execução anterior.

## Persistência

A tela restaura somente o manifesto mais recente cujo perfil seja
`PRESERVATION_TOTAL_11.6H.1`. O resultado antigo do 11.6H continua preservado
como auditoria, mas não aparece como aprovação do novo modo.

## Escopo

Este marco ainda usa os handles históricos já conhecidos. Ele não reconhece
sozinho pontos em um DWG novo. Depois da aprovação do 11.6H.1, a próxima fase
será o reconhecimento supervisionado de um desenho nunca analisado.

## Validação real do 11.6H.1 — 29/07/2026

A execução `2c25f58e-841e-4b86-9986-bf3d85feb745` foi concluída e aprovada
pelo AutoCAD Core Console:

- perfil: `PRESERVATION_TOTAL_11.6H.1`;
- 272/272 pontos na layer esperada;
- 100/100 componentes na layer esperada;
- 272/272 correspondências com a máscara histórica;
- 1.400 entidades antes e 1.400 depois;
- zero handles ausentes;
- uma referência `CINZA PONTOS` antes e uma depois;
- SHA-256 do DWG original inalterado;
- SHA-256 da máscara histórica inalterado;
- adaptador executado sem ocorrência de `entdel`;
- script de execução carregando apenas o adaptador de preservação.

As rotinas históricas V04, V05 e V06 permanecem copiadas na pasta
`rotinas-originais` exclusivamente como evidência. Mesmo que a V06 contenha
código antigo de limpeza, o arquivo `executar-11.6h1.scr` não a carrega.

O arquivo resultante foi gravado como
`AUTOAIBUILDER_PONTOS_PRESERVADOS_20260729_180103.dwg`. Esse resultado encerra
tecnicamente o 11.6H.1 e libera o início do reconhecimento supervisionado de um
DWG novo.
