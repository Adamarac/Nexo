"""Ajudas de gráfico da Análise: agrupar o excesso em "Outros" e dar cor fixa a cada categoria."""

import pandas as pd

from analytics import theme
from nexo_shared import formatting

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


def maturity_table(assets: list[dict], holdings: list[dict]) -> pd.DataFrame:
    """Uma linha por vencimento, com quantos fundos têm e quanto no mês; vazio quando nenhum fundo tem."""
    held = {item["assetId"]: item for item in holdings}
    missing = float("nan")
    return pd.DataFrame(
        {
            "id": [asset["id"] for asset in assets],
            "maturity": [formatting.parse_date(asset["maturityDate"]) for asset in assets],
            "isin": [asset["isin"] for asset in assets],
            "assetType": [asset["assetType"] for asset in assets],
            "issuerName": [asset["issuerName"] for asset in assets],
            "funds": [held[asset["id"]]["funds"] if asset["id"] in held else missing for asset in assets],
            "value": [held[asset["id"]]["marketValue"] if asset["id"] in held else missing for asset in assets],
        }
    )


def maturity_months(table: pd.DataFrame) -> pd.DataFrame:
    """Soma por mês o que os fundos têm em cada vencimento: CDBs vencem em vários dias do mesmo mês."""
    held = table[table["value"].notna()]
    months = held["maturity"].map(lambda day: day.replace(day=1))
    return (
        held.assign(month=months)
        .groupby("month", as_index=False)
        .agg(value=("value", "sum"), maturities=("value", "size"))
        .sort_values("month")
        .reset_index(drop=True)
    )
