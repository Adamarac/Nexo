"""Ajudas de gráfico da Análise: agrupar o excesso em "Outros" e dar cor fixa a cada categoria."""

import pandas as pd

from analytics import theme

OTHERS = "Outros"


def fold(frame: pd.DataFrame, label: str, value: str, keep: int = 7) -> pd.DataFrame:
    """Mantém as maiores categorias e soma o resto em 'Outros', para nunca passar das cores validadas."""
    grouped = frame.groupby(label, as_index=False)[value].sum().sort_values(value, ascending=False)
    if len(grouped) <= keep + 1:
        return grouped.reset_index(drop=True)
    top, rest = grouped.head(keep), grouped.iloc[keep:]
    other = pd.DataFrame({label: [OTHERS], value: [rest[value].sum()]})
    return pd.concat([top, other], ignore_index=True)


def category_colors(names: list[str]) -> dict[str, str]:
    """Cor na ordem em que as categorias aparecem; 'Outros' é sempre cinza."""
    colors, index = {}, 0
    for name in names:
        if name == OTHERS:
            colors[name] = theme.OTHER
        elif name not in colors:
            colors[name] = theme.SERIES[index % len(theme.SERIES)]
            index += 1
    return colors
