# Nexo Análise — direção de arte

## Referência

Design system "ByeWind / SnowUI" (Figma, comunidade), escolhido pelo usuário. As duas telas do Nexo
usam o mesmo sistema; o que muda é o conteúdo.

## Para quem

Analistas que estudam carteiras de fundos: o que um fundo tem, como o patrimônio evoluiu, quão
concentrado ele é, quem mais tem um ativo e como dois fundos se comparam.

## Regras visuais

- **Página branca, cartões cinza-claro** (`#F7F9FB`), cantos de 16 px, sem borda e sem sombra.
- **Indicadores**: o 1º com fundo azul-claro (`#E3F5FF`), o 4º com fundo lavanda (`#E5ECF6`), os outros
  cinza. Rótulo 14 px, número 24 px semibold, nota 12 px. Sem ícones.
- **Texto** quase preto (`#1C1C1C`) e cinza `#6B7280` para rótulos. Fonte Inter em tudo. Nada menor que
  12 px.
- **Menu lateral branco** com uma linha fina à direita; item ativo com fundo cinza; ícones pequenos e
  monocromáticos.
- **Cabeçalho**: caminho em cinza ("Análise / Fundos"), título 22 px e uma linha de contexto.
- **Etiquetas**: bolinha colorida e texto escuro, sem fundo (nada de "badges").

## Gráficos

- Linhas finas pretas, sem preenchimento, sem grade; barras com cantos arredondados.
- **Rankings são listas escritas**, como "Revenue by Location": nome inteiro (quebra linha, nunca é
  cortado), valor à direita e uma barra fina embaixo. Nomes longos nunca vão para um eixo.
- **Rosca** só para partes de um todo, com até quatro fatias e "Outros", número no meio e legenda em
  lista embaixo.
- Paleta categórica fixa: `#7B8CF8` `#4CC38A` `#F0A04B` `#3AA5EE` `#E57BA9`; "Outros" em `#C4CAD4`.
  Validada contra `#F7F9FB`: separação para daltonismo no limite (ΔE 7,6 entre laranja e verde), por
  isso todo gráfico tem valores escritos ou legenda com números.
- Nunca dois eixos verticais; escala brasileira (R$ mi, R$ bi).

## Conteúdo por página

- **Panorama**: fundos por tipo, onde está o dinheiro (rosca), 10 ativos com mais dinheiro, dispersão
  valor × número de fundos.
- **Análise do fundo**: evolução do patrimônio, medidas resumo, alocação, maiores posições, variação
  mensal, curva de concentração, maiores emissores, dependência dos maiores ativos.
- **Comparar**: medidas lado a lado, base 100, distribuição das variações, alocação, sobreposição e
  ativos em comum.
- **Ativos**: ao abrir, os 100 ativos com mais dinheiro dos fundos; vencimentos do mesmo título numa linha só,
  que abre a lista de datas (vencido ou a vencer); no detalhe, detentores, peso do ativo em quem tem e
  concentração entre detentores.
- **Emissores**: lista pelo CNPJ com o nome mais usado; no detalhe, cada forma de escrever o nome com quantos
  fundos a usam (lista de barras, sem cortar o texto) e a tabela de fundos com o jeito que cada um escreve.

## Honestidade dos números

A variação do patrimônio inclui aplicações e resgates e nunca é chamada de rentabilidade. O patrimônio
da lista de fundos vem do cadastro da CVM e a coluna diz isso.
