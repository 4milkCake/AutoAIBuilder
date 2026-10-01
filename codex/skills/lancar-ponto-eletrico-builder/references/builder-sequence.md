# Sequência observada no Builder

## Antes do lançamento

1. Abrir o projeto de Fiação do pavimento.
2. Conferir a aba `Lançamento`.
3. Conferir a rede `Elétrica`.
4. Definir explicitamente a posição:
   - baixa;
   - média;
   - alta;
   - teto;
   - piso;
   - outra posição aplicável.
5. Selecionar o comando e a peça correta.

O estado de posição foi esquecido em ensaios humanos anteriores. Sempre
verificá-lo antes do primeiro clique de lançamento.

## Peça

Exemplo histórico de tomada média de 20 A:

```text
Pontos de força - Uso geral - 2P+T 20 A - média
```

Não tratar o texto como identidade universal do catálogo. Confirmar o nome
visível na versão instalada.

Tomadas comuns podem ser 10 A ou 20 A. A máscara ou o usuário deve decidir. Não
selecionar peça de potência 2200 W apenas por causa da carga; circuitos e
potências especiais são tratados posteriormente.

## Posicionamento

O ponto gráfico da máscara pode não coincidir com a inserção da peça. Usar:

```text
P_builder =
    TransformacaoGlobal(P_ancora_wcs)
    + OffsetLocal(tipo, peça, altura, orientação)
```

Depois:

1. ativar `Ponto relativo`;
2. informar o deslocamento aprovado;
3. clicar na referência prevista;
4. indicar orientação;
5. validar visualmente.

Não misturar transformação global, offset local e pixel de tela.

## Limite do piloto

- uma tomada média;
- um ponto;
- projeto de teste;
- confirmação humana;
- sem circuitos;
- sem dimensionamento;
- sem lote;
- sem salvamento automático.

