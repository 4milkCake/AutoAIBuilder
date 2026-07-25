# Marco 11.6A — Catálogo seguro de máscaras

O catálogo recebe contratos declarativos antes da integração dos adaptadores
reais. Uma importação combina exatamente dois arquivos:

- um contrato conforme `automation-mask.schema.json`;
- um catálogo conforme `automation-rule-catalog.schema.json`.

Importar ou ativar uma máscara nesta etapa não executa automações, não carrega
bibliotecas, não inicia programas externos e não altera os arquivos JSON de
origem.

## Fluxo da interface

1. Abrir `Máscaras`.
2. Selecionar o JSON da máscara.
3. Selecionar o JSON do catálogo de regras.
4. Executar `Analisar pacote com segurança`.
5. Conferir identidade, versão, regras, parâmetros, saídas, dependências e
   SHA-256.
6. Corrigir qualquer bloqueio antes da importação.
7. Usar `Importar como inativa`.
8. Opcionalmente ativar uma versão para a integração futura.

A ativação é apenas uma seleção no catálogo. O Marco 11.6A não possui registro
de adaptadores e, portanto, nenhuma máscara catalogada é executável.

## Validação de entrada

- somente arquivos locais com extensão `.json`;
- no máximo 1 MiB por arquivo;
- links e pontos de nova análise são rejeitados;
- JSON inválido ou com propriedades desconhecidas é rejeitado;
- todos os campos obrigatórios dos schemas devem estar presentes;
- versões devem usar o formato estável `X.Y.Z`;
- Preview, RC e versões de aplicação incompatíveis não são aceitos;
- identificadores, extensões, parâmetros, referências, saídas e caminhos são
  validados;
- limites de quantidade protegem a análise contra pacotes excessivos;
- a máscara deve referenciar regras existentes e ativas no catálogo fornecido.

Os documentos são normalizados antes do cálculo do SHA-256. Assim, diferenças
apenas de indentação não criam pacotes distintos.

## Versionamento e conflitos

A identidade é `maskId@maskVersion`.

- mesma identidade e mesmo SHA-256: o pacote já está instalado e não é
  duplicado;
- mesma identidade e SHA-256 diferente: conflito bloqueante, sem
  sobrescrita;
- mesmo identificador e versão diferente: ambas podem coexistir;
- somente uma versão de cada identificador pode estar ativa;
- desativar não remove conteúdo e pode ser revertido.

Os arquivos de origem nunca são movidos, renomeados ou alterados. O banco
preserva somente os nomes dos arquivos, os contratos normalizados, metadados e
o hash do pacote; caminhos completos de origem não são armazenados.

## Persistência

O esquema 4 do SQLite adiciona `AutomationMaskCatalog`. Cada registro conserva:

- identidade, versão, nome e disciplina da máscara;
- versão mínima do AutoAIBuilder;
- identidade e versão do catálogo de regras;
- contagens de regras, dependências, parâmetros e saídas;
- suporte declarado a simulação e idempotência;
- JSON normalizado dos dois contratos;
- SHA-256 do pacote;
- nomes dos arquivos de origem;
- estado ativo ou inativo;
- horários de importação e atualização.

A restrição do banco impede duas versões ativas do mesmo identificador.

## Limites intencionais

O catálogo ainda não:

- associa uma máscara a um adaptador;
- avalia as condições declarativas das regras;
- detecta AutoCAD, AltoQi Builder ou outras dependências;
- importa código, scripts, DLLs, macros ou executáveis;
- executa simulação ou aplicação de uma máscara real;
- remove versões catalogadas.

Essas ligações pertencem aos próximos passos do Marco 11.6.
