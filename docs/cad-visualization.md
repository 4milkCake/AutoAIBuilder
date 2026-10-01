# Marco 11.6E.1 — Revisão arquitetônica ampliada

## Resultado

O Marco 11.6E conecta a central de revisão semântica a uma representação
vetorial real do DWG. A planta é exibida no próprio AutoAIBuilder, sob os pontos
elétricos e hidráulicos, sem exigir comparação visual com outra janela.

No ensaio de homologação com
`TESTE_01_MASCARA_AUTOMATICA_V06.dwg`, o pipeline produziu:

- 37.251 elementos gráficos validados;
- 37.430 entidades examinadas, com cobertura vetorial de 99,5%;
- 162 layers;
- 272 pontos semânticos sobrepostos;
- 16 blocos especiais sinalizados para inspeção, sem ocultar a limitação;
- extração pelo AutoCAD Core Console `25.0.171.0.0`.

O desenho possui a maior parte da arquitetura dentro do bloco
`CINZA PONTOS`, com aproximadamente 35.850 entidades internas. A revisão 3 do
pipeline usa o comando nativo de expansão do AutoCAD na cópia técnica para
materializar blocos grandes antes da exportação. O método COM anterior podia
retornar vazio sem informar falha e produzia apenas 581 elementos.

A revisão 3 também registra a quantidade total de entidades lidas, quantas
foram convertidas para primitivas visuais e quantos blocos especiais
permaneceram sem decomposição. O aplicativo apresenta esses dados como um
indicador de cobertura, sem ocultar limitações do desenho.

## Fluxo seguro

1. O serviço valida a extensão e a existência do DWG.
2. Calcula o SHA-256 do arquivo original.
3. Cria uma pasta exclusiva de processamento.
4. Copia o DWG com criação obrigatoriamente nova, sem sobrescrever arquivos.
5. Compara o SHA-256 da cópia com o original.
6. Executa o AutoCAD Core Console somente sobre a cópia técnica.
7. Converte a geometria para o formato interno AIV1.
8. Recalcula o SHA-256 do original depois da extração.
9. Publica o artefato somente se estrutura, limites, layers e geometria forem
   válidos.

O pipeline não salva, renomeia, move nem escreve no DWG original. Uma falha
preserva a pasta técnica e o log para diagnóstico, mas o aplicativo não aceita
o artefato incompleto.

## Cache

O cache fica em:

```text
%LOCALAPPDATA%\AutoAIBuilder\Data\CadVisualization
```

Cada entrada é vinculada à versão do pipeline, ao projeto e ao SHA-256 do DWG.
Assim, uma correção do conversor não reaproveita silenciosamente um artefato
antigo. Antes de reutilizar um resultado, o serviço confere novamente:

- caminho do DWG;
- hash atual;
- manifesto;
- existência e validade do artefato vetorial.

Uma alteração no DWG produz outro hash e obriga uma nova extração.

## Formato AIV1

O artefato de visualização é textual e restrito a tipos conhecidos:

- linha;
- polilinha;
- círculo;
- arco;
- texto;
- caixa de limites para entidades sem representação vetorial direta.

O parser rejeita versão desconhecida, campos inválidos, limites vazios e
arquivos sem primitivas. O formato é interno e não substitui o DWG.

## Recursos do visualizador

Na aba **Análise semântica > Visão geral**:

- **Gerar planta com AutoCAD** cria ou recupera a visualização verificada;
- a roda do mouse e o botão **+** ampliam a planta;
- o botão **−** reduz o zoom;
- o arraste com o botão esquerdo move a planta;
- **Ajustar** restaura o enquadramento;
- **Tudo** mostra a extensão completa do model space;
- **Expandir** abre uma janela maximizada com a planta ocupando a maior parte
  da tela e a lista de pontos mantida na parte inferior;
- a lista **Layers** pesquisa, agrupa e liga ou desliga a geometria por
  arquitetura, elétrica, hidráulica, textos/cotas e outras;
- um clique em um ponto abre sua correção detalhada e o destaca com mira;
- a seleção feita pela tabela ou pela fila de pendências aparece no mesmo mapa
  e centraliza automaticamente o ponto;
- a janela ampliada pode ser redimensionada, permite ajustar a altura da tabela
  e fecha pelo botão **Fechar** ou pela tecla **Esc**.

## Diagnóstico da dependência

O serviço procura o executável oficial do AutoCAD Core Console em caminhos
instalados e confirma o publicador Autodesk antes do uso. Ausência, assinatura
incompatível, timeout, falha do processo ou saída inválida são informados ao
usuário; não existe troca silenciosa por outro motor.

## Limitações conhecidas

- O enquadramento inicial prioriza a área dos pontos semânticos. O botão
  **Tudo** continua disponível para inspecionar quadro, legenda e objetos
  afastados.
- Entidades CAD complexas que não podem ser decompostas com segurança aparecem
  por sua caixa de limites quando o AutoCAD a disponibiliza.
- O visualizador é uma bancada de inspeção e revisão; ainda não edita o DWG.
- A central não aplica máscaras nem envia elementos ao AltoQi Builder.

Esses limites preservam a separação entre visualização supervisionada e futuras
automações que modificarão cópias de trabalho mediante plano e confirmação.
