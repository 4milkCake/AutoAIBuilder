# Marco 11.6G — pré-visualização supervisionada

## Objetivo

O Marco 11.6G transforma o plano estático do 11.6F numa decisão visual antes de
qualquer execução CAD. A tela compara a arquitetura original com a proposta,
mostra os grupos produzidos pelas rotinas históricas e registra aprovação ou
rejeição de cada grupo.

Nenhum AutoLISP é carregado, nenhum comando é enviado ao AutoCAD e nenhum DWG é
gravado nesta fase.

## Comparação antes/depois

- **Antes:** artefato gráfico verificado do DWG, sem sobreposições;
- **Depois — prévia:** a mesma arquitetura com pontos ou componentes do grupo
  selecionado;
- as duas vistas possuem enquadramento sincronizado pelos comandos `Ajustar` e
  `Mostrar tudo`;
- selecionar um grupo atualiza a sobreposição sem modificar o artefato CAD.

## Categorias visuais

| Categoria | Cor | Significado |
|---|---|---|
| Original | cinza | arquitetura protegida |
| Detectado | azul/ciano | resultado das rotinas v01–v04/v07 |
| Proposto | verde | camadas e máscara que só poderiam ser aplicadas depois |
| Corrigido | roxo | revisão humana que prevalece sobre a detecção |
| Ignorado | amarelo | pendência mantida fora de aplicação automática |

## Grupos auditáveis

O plano contém:

1. arquitetura original;
2. pontos elétricos detectados;
3. pontos hidráulicos detectados;
4. componentes associados;
5. correções humanas;
6. camadas semânticas propostas;
7. máscara automática proposta;
8. pendências temporariamente ignoradas.

A arquitetura original é marcada como `PROTEGIDO`. Grupos vazios não exigem
decisão. Os demais começam como `PENDENTE` e podem ser `APROVADO` ou
`REJEITADO`, com observação opcional.

## Identidade e integridade

O plano recebe um SHA-256 determinístico composto por projeto, conjunto
semântico, impressão digital dos CSVs e SHA-256 do DWG visualizado. Antes de
mostrar o plano como íntegro, o serviço recalcula o hash do arquivo original em
modo somente leitura e compara com o hash do artefato CAD.

As decisões são persistidas em `AutomationPreviewDecisions`, identificadas pelo
SHA-256 do plano e pelo grupo. Se a origem mudar, nasce outro plano e decisões
antigas não são reaproveitadas silenciosamente.

## Condição para o 11.6H

O plano só aparece como `PLANO APROVADO PARA O 11.6H` quando:

- a integridade do DWG original está confirmada;
- todos os grupos obrigatórios foram decididos;
- nenhum grupo está rejeitado.

Mesmo nessa condição, o 11.6G não oferece comando de execução. O 11.6H deverá
usar uma cópia técnica, revalidar hashes e comparar o resultado com a máscara
histórica.
