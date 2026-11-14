# Nexo Console — direção de arte

## Referência

Design system "ByeWind / SnowUI" (Figma, comunidade), escolhido pelo usuário. É o mesmo sistema da
Análise; o Console muda o conteúdo, não o estilo.

## Para quem

Desenvolvedores que operam o Nexo: o pipeline está saudável e, se não está, onde está o problema?

## Regras visuais

- Página branca, cartões `#F7F9FB` com cantos de 16 px, sem borda e sem sombra.
- Indicadores no padrão do design: 1º azul-claro, 4º lavanda, os outros cinza; sem ícones.
- Inter em tudo, texto `#1C1C1C`, rótulos `#6B7280`, nada menor que 12 px.
- Menu lateral branco com ícones monocromáticos, em dois grupos: Operação e Infraestrutura.
- Estados escritos (`OK`, `ATENÇÃO`, `SEM SINAL`, `FALHA`) com uma bolinha colorida ao lado; sem fundo.
- Serviços em blocos brancos dentro do cartão cinza, com nome, estado e detalhe.

## Gráficos

- Linhas finas, sem preenchimento e sem grade; horários de Brasília.
- Rankings (motivos de rejeição, logs por nível, eventos frequentes, processamento por instância) são
  listas escritas com barra fina: nomes nunca cortados.
- Rosca para espaço por tabela; anel para um percentual isolado (memória, cache, disco).
- Mesma paleta da Análise: `#7B8CF8` `#4CC38A` `#F0A04B` `#3AA5EE` `#E57BA9`, "outras" em `#C4CAD4`.

## Dados

Cada cartão diz de onde veio o dado e a hora da leitura. Fonte fora aparece como `FORA`, nunca como zero.
Containers parados ficam numa lista à parte, "sem sinal".
