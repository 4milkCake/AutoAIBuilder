---
name: lancar-ponto-eletrico-builder
description: Planejar, executar e auditar o lançamento supervisionado de um único ponto elétrico no AltoQi Builder por Computer Use. Usar somente em projeto de teste ou cópia recuperável, depois de aprovar a identificação da máscara e a calibração DWG–Builder, para conferir estado da interface, rede, posição, peça, amperagem, ponto relativo e orientação com confirmação humana e evidência antes/depois.
---

# Lançar ponto elétrico no Builder

Executar um ponto por vez. Tratar qualquer divergência visual como motivo para
parar.

## Pré-condições

Exigir:

- projeto de teste ou cópia recuperável;
- pavimento e disciplina identificados;
- ponto semântico aprovado;
- contrato de calibração aprovado pelo usuário;
- offset local `VALIDATED` para a combinação de peça e orientação;
- peça e amperagem confirmadas;
- Builder aberto na tela esperada;
- autorização para controlar a interface.

Se qualquer item faltar, produzir apenas o plano e não clicar.

## Preparar Computer Use

Usar a skill `computer-use`. Antes de controlar o Windows:

1. ler integralmente a skill `computer-use`;
2. inicializar o runtime indicado por ela;
3. ler `sky.documentation("guidance")`;
4. ler `sky.documentation("confirmations")`;
5. identificar e fixar a janela-alvo do AltoQi Builder.

Não substituir o runtime oficial por scripts próprios de mouse/teclado.

## Planejar

Ler [builder-sequence.md](references/builder-sequence.md) e produzir:

- ID/handle e descrição do ponto;
- coordenada WCS;
- transformação e coordenada Builder;
- peça e amperagem;
- posição/altura;
- orientação;
- offset local e origem da evidência;
- sequência visível de ações;
- portões de confirmação;
- caminho de cancelamento e desfazer.

Mostrar o plano antes de executar.

## Executar um ponto

1. Capturar o estado inicial.
2. Confirmar projeto, pavimento e aba de Fiação.
3. Confirmar aba `Lançamento` e rede `Elétrica`.
4. Definir explicitamente posição/altura; não confiar no estado anterior.
5. Selecionar o comando e a peça confirmada.
6. Usar `Ponto relativo`.
7. Aplicar a coordenada global e o offset local aprovado.
8. Indicar a orientação validada.
9. Parar antes de qualquer confirmação irreversível exigida pelo runtime.
10. Capturar o resultado e solicitar aprovação visual.
11. Registrar a auditoria definida em
    [execution-audit.md](references/execution-audit.md).

## Portões

- Nunca escolher 10 A ou 20 A apenas pela potência aparente.
- Nunca usar offset `INFERRED` ou `PENDING`.
- Nunca usar coordenadas de pixels como coordenadas do projeto.
- Nunca avançar se a interface mudar, ocultar o alvo ou mostrar diálogo
  inesperado.
- Nunca salvar, sobrescrever, fechar, publicar ou iniciar lote sem autorização
  explícita.
- Nunca ampliar de um ponto para vários por conta própria.
- Manter disponível o caminho de desfazer.

## Estado atual

O primeiro piloto é uma tomada média. Esquerda e baixo possuem evidência
histórica; direita e cima continuam bloqueadas enquanto não forem testadas
realmente no Builder.

