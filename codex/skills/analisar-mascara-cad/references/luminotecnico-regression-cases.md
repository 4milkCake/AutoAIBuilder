# Casos de regressão luminotécnica

Usar estes casos somente como oráculos de regressão. Não copiar contagens,
ambientes, handles, direções ou transformações para outro DWG.

## Regras confirmadas nos dois desenhos

- Interpretar textos curtos como `F1`, `S1`, `V1`, `H2` e `Q3` como códigos de
  comando quando o contexto indicar acionamento. Eles não definem o tipo da
  luminária.
- Classificar o tipo pela combinação de legenda, assinatura geométrica e
  contexto da ocorrência. O mesmo símbolo continua sendo o mesmo tipo mesmo
  quando o código de comando muda.
- Tratar ângulo de facho, como `12°` ou `25°`, como característica fotométrica;
  não convertê-lo em direção de montagem.
- Marcar luminária central de teto como `NOT_APPLICABLE` para direção, salvo
  evidência funcional contrária.
- Tratar ponto amarelo hachurado associado a LED como ponto de energia para
  iluminação sem caixa quando a legenda/contexto e a confirmação profissional
  sustentarem essa classe. Não inventar peça ou método do Builder.
- Subdividir perfil em L em duas retas e retângulo fechado em quatro. Gerar
  uma âncora no centro de cada reta e preservar o percurso pai.
- Resolver arandela individualmente como `FACE_DIRECTION`. Aprovação de tipo e
  quantidade não aprova automaticamente a direção.
- Delimitar ambientes pela arquitetura completa e pela continuidade espacial.
  Não nomear um ambiente apenas pelo código de comando ou pelo recorte da
  captura; vazios, jardins, áreas de serviço e pés-direitos duplos podem ser
  confundidos com cômodos adjacentes.
- Agrupar entidades coincidentes somente quando representarem a mesma posição
  física e houver evidência explícita. Preservar todos os handles de origem.

## Luminotécnico do térreo

Sessão:
`artifacts/codex-first/recognition/luminotecnico-terreo/20260731-125222-8a9d3fa654be48e79793b18df74dbe9e`

Estado esperado: `APPROVED_BY_USER`.

Oráculo consolidado:

- 15 itens de legenda revisados;
- 60 ocorrências fixas;
- 23 arandelas reais no muro externo; exemplar da legenda excluído;
- 20 segmentos retos de perfil/LED;
- 13 entidades de ponto LED sem caixa agrupadas em 12 posições físicas;
- um perfil em L dividido em 2 segmentos;
- um perfil retangular dividido em 4 segmentos;
- 7 degraus tratados como segmentos independentes;
- dois handles coincidentes de ponto especial agrupados como uma posição;
- zero gravações no DWG original e SHA-256 preservado.

Correções semânticas importantes:

- os conjuntos magenta do lavabo e dos banheiros são spots Picco recuados,
  não spots quádruplos;
- os dois símbolos laranja do quarto são spots PAR20 direcionáveis;
- `S4` representa lustre/pendente modelo a definir;
- `G6` está no jardim e é balizador de solo;
- os plafons azuis próximos à cozinha pertencem à área de serviço;
- o perfil na borda da área gourmet pertence à sala;
- o cortineiro esquerdo é independente do perfil em L da sala.

## Luminotécnico do segundo pavimento

Sessão:
`artifacts/codex-first/recognition/luminotecnico-2-pavimento/20260802-141659-9809a0c525454bef9166f801c8998e40`

Estado esperado: `APPROVED_BY_USER`.

Oráculo consolidado:

- 15 itens de legenda reutilizados e conferidos visualmente;
- 57 luminárias fixas;
- 17 pontos de energia para LED sem caixa;
- 12 segmentos retos de perfil/cortineiro;
- 86 unidades de futura colocação em 15 regiões/ambientes;
- 78 registros aprovados e 8 corrigidos; nenhum pendente;
- 2 arandelas V2 com direção efetiva `DOWN` e ângulo efetivo `270°`;
- 6 registros antes atribuídos a `QUARTO_S` corrigidos para
  `PE_DIREITO_DUPLO_SALA_TERREO`;
- zero gravações no DWG original e SHA-256 preservado.

Contagem fixa por item da legenda:

| Item | Quantidade |
|---:|---:|
| 1 — plafon LED de teto | 2 |
| 2 — spot Picco recuado | 6 |
| 5 — lustre/pendente a definir | 2 |
| 6 — lâmpada LED PAR30 | 36 |
| 11 — arandela | 2 |
| 13 — spot PAR20 direcionável | 5 |
| 15 — plafon difuso com spots | 4 |

Todos os demais itens tiveram zero ocorrência fixa nesse desenho.

## Teste de regressão

Executar o validador sobre os dois pacotes:

```powershell
python scripts/validate_mask_package.py <pasta-da-sessao> --require-approved
```

Adicionar `--source-dwg <origem.dwg>` quando o arquivo original estiver
disponível para reler o SHA-256. Um teste passa somente se os registros forem
coerentes; os números acima continuam sendo conferidos separadamente como
oráculos específicos dessas sessões.
