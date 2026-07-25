# Recepção da automação existente

Este documento define o pacote mínimo para incorporar ao AutoAIBuilder o
processo desenvolvido no ChatGPT Work sem reconstruí-lo por suposição.

## Como fornecer

Use uma destas opções:

1. anexar os arquivos diretamente à conversa atual;
2. exportar a conversa como PDF, HTML, Markdown ou texto e informar o caminho;
3. reunir os materiais em uma pasta local e informar o caminho completo;
4. fornecer os itens gradualmente, mantendo a identificação de cada versão.

O material será analisado no local. Os originais não serão movidos, renomeados
ou apagados.

## Pacote mínimo

| Prioridade | Material | Conteúdo esperado |
|---|---|---|
| Obrigatório | Conversa ou resumo operacional completo | Sequência da criação da máscara e das identificações |
| Obrigatório | Arquivos utilizados | Desenhos, imagens, tabelas, JSON ou outros insumos efetivamente usados |
| Obrigatório | Resultados de referência | Máscara e pontos identificados considerados corretos |
| Obrigatório | Regras conhecidas | Critérios para tomadas, pontos elétricos e pontos hidráulicos |
| Importante | Correções e exceções | Casos em que o fluxo errou e como foi corrigido |
| Importante | Prompts finais | Versões mais recentes das instruções que produziram os melhores resultados |
| Importante | Dependências | Aplicativos, versões, comandos ou formatos esperados |
| Se existir | Scripts, macros ou código | Apenas para leitura e revisão; não serão executados diretamente |
| Se existir | Gravações ou capturas | Evidência da sequência de interação e dos resultados |

## Informações que devem acompanhar cada exemplo

- identificador ou nome do caso;
- arquivo de entrada;
- resultado esperado;
- disciplina: elétrica, hidráulica ou ambas;
- versão aproximada do processo que o produziu;
- etapas que exigiram intervenção manual;
- limitações ou erros conhecidos;
- indicação do que foi considerado aprovado.

## Cuidados antes do envio

- remover senhas, tokens, chaves de API e credenciais;
- remover dados pessoais que não sejam necessários;
- não incluir instaladores ou programas;
- não alterar os arquivos apenas para adequá-los ao AutoAIBuilder;
- preservar, quando possível, os exemplos que falharam;
- manter as versões finais e intermediárias claramente identificadas.

## Formatos aceitos para análise

- documentação: `.txt`, `.md`, `.html`, `.pdf`, `.docx`;
- contratos e dados: `.json`, `.csv`, `.xlsx`;
- imagens: `.png`, `.jpg`, `.jpeg`;
- desenhos e derivados: informar o formato e o aplicativo de origem;
- código: fornecer os arquivos-fonte e sua finalidade;
- conversas: exportação integral ou seleção que preserve a ordem das mensagens.

Arquivos executáveis, DLLs, scripts e macros serão tratados como material
não confiável e somente inspecionados. O recebimento não autoriza sua execução.

## Resultado da primeira análise

Após o recebimento, será produzido um relatório com:

- inventário e SHA-256 dos artefatos;
- reconstrução do fluxo atual;
- matriz `reutilizar / normalizar / adaptar / completar / bloquear`;
- mapa para máscara, regras, parâmetros, saídas e validadores;
- lista objetiva das lacunas;
- proposta do primeiro caso de regressão;
- definição do adaptador de simulação;
- itens que continuarão dependentes de confirmação humana;
- itens que ficarão adiados para a integração CAD.

Nenhuma aplicação sobre o projeto real ocorrerá durante essa análise.
