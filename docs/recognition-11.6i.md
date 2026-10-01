# Marco 11.6I — Reconhecimento supervisionado de um novo DWG

## Objetivo

Iniciar a generalização do AutoAIBuilder para desenhos que não possuem os
handles históricos do projeto usado nos marcos 11.6F–11.6H.1.

O 11.6I é deliberadamente somente leitura. Ele não cria máscara, não troca
layers e não grava comandos no DWG.

## Fluxo disponível

1. o usuário seleciona um DWG;
2. o arquivo original recebe um SHA-256;
3. uma cópia técnica é criada na área de dados do AutoAIBuilder;
4. o AutoCAD Core Console inventaria as entidades do Model Space;
5. os blocos `INSERT` são comparados com os exemplos da base semântica
   validada;
6. cada candidato recebe classificação proposta, confiança numérica e motivo;
7. a planta é renderizada sobre outra cópia técnica;
8. os candidatos aparecem sobre o DWG;
9. o usuário pode aprovar, rejeitar ou corrigir;
10. cada decisão é persistida em uma auditoria separada.

## Dados preservados no inventário

- handle;
- tipo da entidade;
- layer original;
- nome efetivo do bloco;
- coordenadas X, Y e Z;
- rotação;
- escalas X, Y e Z;
- texto, quando a entidade possuir conteúdo textual.

O inventariador não chama `EXPLODE`, `ENTMOD`, `ENTDEL`, `QSAVE` nem qualquer
outro comando de mutação.

## Estratégia inicial de reconhecimento

A primeira versão usa três níveis:

1. **nome exato de bloco validado e precisão histórica calibrada** — confiança
   proporcional à frequência com que esse bloco realmente representou pontos;
2. **layer semântica já conhecida** — confiança alta;
3. **vocabulário técnico no nome do bloco/layer** — confiança média e revisão
   obrigatória.

Blocos sem correspondência continuam visíveis como não classificados. Isso é
intencional: o sistema não inventa uma classificação apenas para aumentar a
taxa aparente de acerto.

O nome do bloco sozinho não recebe confiança alta. A calibração também considera
as ocorrências negativas do mesmo símbolo, como exemplares usados em legendas ou
componentes auxiliares.

## Revisão humana

Para cada candidato a interface permite:

- aprovar a proposta;
- rejeitar o símbolo;
- corrigir disciplina;
- corrigir código e descrição;
- informar layer de destino;
- informar altura;
- registrar observação.

A revisão altera somente o manifesto da sessão. O DWG não é modificado.

## Artefatos

Cada sessão é gravada em:

```text
<dados>/Recognition/<project-id>/<data-hora>-<session-id>/
├── entrada-verificada.dwg
├── inventario.aar
├── autoaibuilder-recognition-inventory.lsp
├── autoaibuilder-recognition-inventory.scr
├── core-console.log
├── manifesto-11.6i.txt
├── session.json
└── auditoria-revisao.jsonl
```

O último arquivo existe somente depois da primeira revisão.

## Limite conhecido desta entrega

O detector inicial considera referências de bloco `INSERT` existentes no Model
Space. Símbolos exclusivamente desenhados como linhas soltas e símbolos
profundamente aninhados dentro de um bloco composto ainda exigirão uma segunda
camada de detecção geométrica. Essa limitação é mostrada pelo número total de
entidades, blocos inventariados e candidatos, sem ocultar a diferença.

## Validação técnica

- compilação Debug e Release aprovadas;
- 136 testes automatizados aprovados;
- inventário real executado com o AutoCAD Core Console;
- SHA-256 do original confirmado depois da leitura;
- 1.400 entidades e 342 blocos `INSERT` observados no desenho de validação;
- calibração real concluída com os 272 handles históricos;
- 245 candidatos classificados com alta confiança e 97 enviados para revisão;
- zero comandos de mutação no manifesto.

## Validação com DWG inédito

Um DWG arquitetônico de outro projeto, nunca usado na base histórica, foi lido
integralmente em modo somente leitura:

- 9.094 entidades inventariadas;
- 139 blocos `INSERT`;
- 139 candidatos encaminhados para revisão;
- zero candidatos classificados com alta confiança;
- 8.055 primitivas gráficas renderizadas em 123 layers;
- 88,57% de cobertura gráfica;
- SHA-256 do original confirmado antes e depois da leitura.

O resultado confirma que o mecanismo é seguro e conservador, mas também mostra
que a primeira base semântica ainda não generaliza para blocos anônimos
`A$C...`, símbolos e nomes de layers usados por outro arquiteto. O sistema não
inventou classificações para aumentar a taxa aparente de acerto.

O teste também revelou e permitiu corrigir um laço na codificação de `%` nos
dois exportadores AutoLISP. Um teste automatizado de regressão protege agora
essa correção.

## Correção espacial 11.6I.1A

O ensaio com `Elétrico - Maíra e Pedro.dwg` demonstrou que o ponto DXF bruto
de uma referência de bloco nem sempre coincide com a geometria exibida. Alguns
arquitetos usam blocos cujo ponto-base está a dezenas de milhares de unidades
do símbolo. O desenho explodido pelo visualizador aparecia na posição correta,
mas o marcador usava esse ponto-base remoto.

O contrato espacial do inventário passou para `AAR|2` e agora:

- converte pontos e direções de OCS para WCS;
- mantém a coordenada de inserção original para auditoria;
- calcula os limites geométricos da definição do bloco;
- aplica escala, rotação e espelhamento ao transformar esses limites para WCS;
- usa a inserção quando ela pertence à geometria do bloco;
- usa o centro geométrico quando o ponto-base está claramente fora do bloco;
- registra `INSERTION_WCS` ou `BOUNDS_CENTER_WCS` em cada candidato;
- grava no manifesto quantas âncoras precisaram ser corrigidas.

O exportador visual também converte linhas, polilinhas, círculos, arcos e textos
para WCS. O cache gráfico foi incrementado para a versão 5, impedindo o uso de
uma renderização antiga com o contrato anterior.

### Ensaio real da correção

O AutoCAD Core Console executou os dois exportadores sobre uma cópia técnica:

- 1.849 entidades e 547 referências de bloco inventariadas;
- 411 referências conservaram o ponto de inserção WCS;
- 136 receberam âncora geométrica corrigida;
- candidatos dentro dos limites da planta aumentaram de 338 para 422;
- símbolos como `balizador de parede`, antes remotos, foram trazidos para a
  geometria efetivamente exibida;
- 8.421 primitivas visuais exportadas;
- limites WCS idênticos aos do desenho de referência;
- nenhum comando de gravação do DWG.

Esta correção trata o alinhamento espacial. A interpretação de formatação e
codificação de `TEXT`/`MTEXT` pertence ao 11.6I.1B. A descoberta semântica de
símbolos profundamente aninhados será completada junto da leitura da legenda,
nos marcos 11.6I.1C e 11.6I.1D.

### Ajustes de usabilidade após a validação do 11.6I.1A

A validação do usuário confirmou tomadas, balizadores, seleção da fila para o
mapa e estabilidade dos marcadores durante zoom e deslocamento. Antes do
11.6I.1B foram incorporados dois ajustes:

- clicar em um marcador agora seleciona e rola automaticamente a fila até a
  linha correspondente, inclusive se ela estava fora do filtro atual;
- o DWG passou a ocupar uma proporção maior da tela e um divisor arrastável
  permite redistribuir espaço entre desenho e fila;
- os controles de zoom, enquadramento e visualização completa ficaram
  disponíveis diretamente no cabeçalho;
- o botão `Expandir` abre uma janela maximizada com o DWG e a fila de
  candidatos, também separados por um divisor ajustável.

Os blocos genéricos ainda apresentados como candidatos não são ocultados por
heurística nesta etapa. Eles serão filtrados com evidência do próprio desenho
durante a interpretação da legenda em 11.6I.1C/11.6I.1D.

## Correção textual 11.6I.1B

O exportador gráfico passou ao contrato `AIV|2` e grava seu artefato
explicitamente em UTF-8. Isso elimina a leitura incorreta de acentos em layers,
TEXT e MTEXT.

Para cada texto, o contrato agora preserva:

- tipo original (`TEXT`, `MTEXT` ou `ATTRIB`);
- ponto de ancoragem convertido para WCS;
- ancoragem horizontal e vertical;
- altura;
- largura de composição do MTEXT;
- rotação;
- nome do estilo textual;
- conteúdo completo, incluindo todos os fragmentos DXF de MTEXT longo.

Antes da renderização, o AutoAIBuilder interpreta os códigos internos do
AutoCAD:

- `\P` é convertido em quebra de linha;
- comandos de fonte, cor, altura, espaçamento e alinhamento deixam de aparecer
  como texto literal;
- grupos `{...}` são removidos sem remover seu conteúdo;
- frações empilhadas são apresentadas no formato `1/2`;
- escapes Unicode são decodificados;
- `%%d`, `%%p` e `%%c` viram `°`, `±` e `Ø`;
- espaços e controles residuais são normalizados.

O renderizador usa a âncora, a largura e a rotação para posicionar o texto no
DWG. O cache gráfico foi incrementado para a versão 6, obrigando a geração de
um artefato novo.

### Ensaio real

O AutoCAD Core Console executou o AIV v2 sobre a cópia técnica de
`Elétrico - Maíra e Pedro.dwg`:

- 8.421 primitivas gráficas;
- 306 registros de texto;
- 166 MTEXT e 140 TEXT;
- zero caracteres de substituição `�`;
- acentos como `conduíte`, `iluminação`, `alteração` e `eletrônica`
  preservados no artefato UTF-8;
- 148 textos com formatação interna encaminhados ao normalizador;
- original não gravado.

Fontes SHX proprietárias que não estejam instaladas continuam usando uma fonte
de leitura substituta, sem corromper o conteúdo textual. A próxima etapa é
11.6I.1C, dedicada à localização e interpretação da legenda do projeto.

## Interpretação da legenda 11.6I.1C

O inventário `AAR|2` já preservava textos, blocos, coordenadas WCS e limites
geométricos. O 11.6I.1C reutiliza esses dados e não introduz um segundo leitor
do DWG.

### Descoberta e pareamento

O interpretador:

1. normaliza `TEXT`, `MTEXT` e `ATTRIB` com o mesmo tratamento textual do
   visualizador;
2. exige um cabeçalho explícito `LEGENDA` ou `SIMBOLOGIA`;
3. estima a escala útil do desenho com percentis, reduzindo a influência de
   elementos remotos;
4. delimita a faixa espacial dos cabeçalhos encontrados;
5. seleciona descrições com vocabulário elétrico, hidráulico ou de iluminação;
6. associa cada descrição ao bloco `INSERT` disponível mais próximo;
7. impede que uma descrição ou símbolo seja usado em mais de um par;
8. rejeita contradições semânticas explícitas, como um bloco denominado
   `PURIFICADOR` pareado com texto de fita de LED.

O resultado é persistido em
`catalogo-legenda-11.6i.1c.json`, dentro da pasta auditável da análise. O
manifesto registra cabeçalhos, pares, exemplares excluídos da fila e candidatos
externos relacionados à legenda.

### Uso conservador na classificação

A legenda do próprio desenho tem prioridade sobre um nome de bloco histórico,
mas não é autoaprovada:

- um símbolo com descrição única e compatível com a base recebe no máximo 82%;
- uma descrição sem código semântico conhecido recebe 74%;
- o mesmo bloco associado a descrições diferentes recebe 45% e fica ambíguo;
- todos permanecem pendentes até a revisão humana;
- os exemplares da própria legenda não aparecem como pontos da planta;
- itens sem par permanecem visíveis como não classificados.

Esse limite evita transformar um pareamento espacial plausível em verdade
definitiva antes de o usuário conferir o quadro.

### Interface

A lateral da tela apresenta:

- estado da detecção;
- quantidade de cabeçalhos, pares e descrições não pareadas;
- dicionário rolável `símbolo → descrição`;
- evidência do pareamento;
- aviso de que os exemplares da legenda foram removidos da fila.

A fila ganhou a coluna `Legenda` e o filtro
`Identificados pela legenda`. Ao selecionar uma ocorrência, a revisão mostra
tanto a descrição local quanto a justificativa completa da classificação.

### Ensaio real somente leitura

O interpretador foi executado diretamente sobre o inventário já produzido no
11.6I.1B para `Elétrico - Maíra e Pedro.dwg`, sem abrir ou gravar o desenho:

- 1.849 entidades inventariadas;
- 3 cabeçalhos de legenda/simbologia;
- 187 entidades dentro da faixa espacial delimitada;
- 28 descrições técnicas candidatas;
- 12 pares símbolo–descrição propostos;
- 16 descrições mantidas sem par;
- 1 associação contraditória descartada;
- zero comandos de mutação.

Entre os pares recuperados estão interruptor paralelo, tomadas baixa simples e
dupla, ponto para internet, tomada para coifa/depurador, saída de som, ponto
elétrico de piso, arandela e balizador. A validação visual do usuário permanece
necessária antes do 11.6I.1D, que aprofundará blocos aninhados e símbolos
constituídos por geometria solta.

## Hierarquia e assinaturas geométricas 11.6I.1D

O contrato de inventário foi elevado de `AAR|2` para `AAR|3`. Cada registro
agora pode carregar:

- profundidade da expansão;
- identificador do bloco raiz;
- caminho estável dentro da hierarquia;
- nome bruto do bloco;
- assinatura estrutural da geometria;
- quantidade de primitivas e de blocos internos.

A rotina AutoLISP usa `EXPLODE` exclusivamente na cópia técnica criada pela
sessão de reconhecimento. Ela percorre os componentes até oito níveis, registra
os descendentes e encerra sem salvar o desenho. O arquivo original permanece
protegido pela verificação de SHA-256 feita antes e depois da análise.

### Correção da posição visual

Alguns blocos do desenho de referência possuíam um ponto de inserção remoto,
embora sua geometria visível estivesse na planta. O
`CadExpandedGeometryAnchorResolver` agrupa as primitivas expandidas pelo bloco
raiz e, quando a inserção está fora dos limites gráficos reais, usa o centro
dessa geometria como âncora de revisão.

Sólidos e hachuras são excluídos desse cálculo porque podem carregar limites
desproporcionais. A coordenada original não é descartada: raiz, caminho e
inserção permanecem na evidência auditável. A nova origem é registrada como
`EXPANDED_GEOMETRY_CENTER_WCS`.

### Pareamento conservador

O interpretador passou a considerar assinatura estrutural e identidade do bloco
em conjunto. Símbolos aninhados podem ser associados à legenda mesmo quando o
`INSERT` de nível superior não possui uma coordenada útil. Componentes de
geometria solta só podem virar modelo quando a mesma assinatura é confirmada por
um símbolo estruturado em outro ponto do inventário; sem essa confirmação, não
há classificação automática.

Essa regra separou dois símbolos visualmente parecidos que haviam sido
confundidos no 11.6I.1C:

- `ENTRADA DE GÁS CANALIZADO` usa o bloco raiz `18E7D`;
- `SAÍDA DE SOM` usa outra identidade geométrica, com bloco raiz `18E8B`.

As associações continuam limitadas a no máximo 82% e exigem validação humana.
Elementos expandidos sem legenda ou perfil conhecido são ignorados, evitando
transformar milhares de linhas arquitetônicas em falsos candidatos.

### Evidência na interface

A fila de candidatos apresenta a coluna `Origem`. O editor detalhado mostra:

- reconhecimento pela identidade do bloco ou por assinatura estrutural;
- reconhecimento por geometria solta, quando houver;
- profundidade e caminho hierárquico;
- assinatura geométrica usada;
- descrição e evidência vindas da legenda.

O manifesto da sessão registra a versão `AAR|3`, inserções expandidas,
âncoras corrigidas, modelos de geometria solta e candidatos efetivamente
associados por esses modelos.

### Ensaio real somente sobre cópia técnica

O AutoCAD Core Console 2025 processou a cópia técnica do desenho de referência:

- 9.421 registros no inventário;
- 1.849 entidades de nível superior;
- 7.572 componentes expandidos;
- 581 inserções, incluindo 34 aninhadas;
- 8.051 registros geométricos com limites válidos;
- 40 descrições técnicas;
- 28 pares símbolo–descrição;
- 12 descrições mantidas sem par;
- 511 candidatos conservadores no serviço;
- 161 candidatos relacionados à legenda;
- 156 âncoras corrigidas pela geometria expandida;
- zero modelos de geometria solta aplicados sem confirmação;
- zero alterações no DWG original.

Foram recuperadas variações de tomadas por altura, tomada com interruptor,
carro elétrico, antena de TV, internet, coifa, bomba de hidromassagem, cortina
motorizada, quadro de distribuição, gás, som e diferentes pontos de iluminação.
O marco não declara cobertura completa: as 12 descrições não pareadas permanecem
como pendência explícita para a conferência visual.
