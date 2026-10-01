# Decisão de arquitetura — visualização CAD inicial

## Decisão

A primeira geração do visualizador arquitetônico do AutoAIBuilder usará o
AutoCAD 2025 instalado como mecanismo de leitura e conversão do DWG.

O AutoAIBuilder não dependerá diretamente de comandos do AutoCAD na camada de
interface. A integração será protegida por um contrato próprio de motor de
visualização, permitindo trocar o AutoCAD por RealDWG, ODA ou outro mecanismo
homologado sem reconstruir a central de revisão.

## Motivos

- aproveitar o AutoCAD oficial já instalado e licenciado no computador;
- preservar a fidelidade de blocos, textos, layers e objetos existentes;
- reaproveitar as rotinas AutoLISP e os relatórios já validados;
- entregar zoom, movimentação e sobreposição dos pontos mais cedo;
- validar o produto antes de assumir o custo de um SDK DWG incorporado.

## Mecanismo disponível

Foi localizado:

- `D:\Autodesk\AutoCAD 2025\accoreconsole.exe`;
- versão de arquivo `25.0.171.0.0`;
- assinatura digital válida de `Autodesk, Inc.`.

O Core Console será tratado como dependência externa detectável. A ausência,
incompatibilidade ou falha dessa dependência deve produzir diagnóstico claro,
nunca uma substituição silenciosa.

## Limites de segurança

- o DWG original nunca será aberto para gravação pelo pipeline;
- cada processamento usará uma cópia técnica identificada por SHA-256;
- saídas gráficas e metadados serão gerados em cache próprio;
- a interface apenas consumirá artefatos validados;
- nenhum comando de aplicação de máscara será executado durante a visualização;
- nenhum resultado será enviado ao Builder;
- arquivos temporários não serão promovidos a definitivos sem validação.

## Experiência pretendida

O visualizador deverá oferecer:

- planta arquitetônica como fundo;
- pontos semânticos alinhados às coordenadas do DWG;
- zoom pela roda do mouse;
- movimentação por arraste;
- ajustar desenho à tela;
- controle de layers;
- seleção nos dois sentidos: mapa para editor e editor para mapa;
- destaque e centralização do ponto em revisão;
- indicação de processamento e de falhas do motor CAD.

## Estratégia de evolução

O AutoCAD é a primeira implementação do contrato, não uma dependência
permanente da interface. Quando limitações técnicas, comerciais ou de
distribuição justificarem a mudança, um leitor DWG incorporado poderá ser
adicionado mantendo o mesmo modelo de visualização e revisão.
