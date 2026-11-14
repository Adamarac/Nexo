"""Medidas calculadas a partir dos dados da API: evolução do patrimônio, concentração e sobreposição.

Atenção: a variação do patrimônio inclui aplicações e resgates, então não é rentabilidade."""

from dataclasses import dataclass

import pandas as pd


@dataclass(frozen=True)
class NetWorthSummary:
    months: int
    first_month: pd.Timestamp | None
    last_month: pd.Timestamp | None
    latest: float | None
    change_month: float | None
    change_12m: float | None
    change_total: float | None
    mean_change: float | None
    std_change: float | None
    best_change: float | None
    worst_change: float | None
    max_drawdown: float | None


@dataclass(frozen=True)
class Concentration:
    positions: int
    top1: float | None
    top5: float | None
    top10: float | None
    hhi: float | None
    effective_count: float | None


def net_worth_series(history: list[dict]) -> pd.Series:
    """Patrimônio por mês. Com várias classes no mesmo mês, fica a de maior patrimônio (a mesma regra da API)."""
    frame = pd.DataFrame(history)
    if frame.empty or "netWorth" not in frame:
        return pd.Series(dtype=float)
    frame["referenceDate"] = pd.to_datetime(frame["referenceDate"])
    series = frame.groupby("referenceDate")["netWorth"].max().dropna().astype(float)
    return series[series > 0].sort_index()


def monthly_changes(series: pd.Series) -> pd.Series:
    return series.pct_change().dropna()


def drawdown(series: pd.Series) -> pd.Series:
    """Quanto o patrimônio está abaixo do maior valor já alcançado até cada mês (0 = no topo)."""
    return series / series.cummax() - 1


def summarize(series: pd.Series) -> NetWorthSummary:
    if series.empty:
        return NetWorthSummary(0, None, None, None, None, None, None, None, None, None, None, None)

    changes = monthly_changes(series)
    year_ago = series[series.index <= series.index[-1] - pd.DateOffset(months=12)]
    return NetWorthSummary(
        months=len(series),
        first_month=series.index[0],
        last_month=series.index[-1],
        latest=float(series.iloc[-1]),
        change_month=_value(changes.iloc[-1]) if len(changes) else None,
        change_12m=float(series.iloc[-1] / year_ago.iloc[-1] - 1) if len(year_ago) else None,
        change_total=float(series.iloc[-1] / series.iloc[0] - 1) if len(series) > 1 else None,
        mean_change=_value(changes.mean()) if len(changes) else None,
        std_change=_value(changes.std()) if len(changes) > 1 else None,
        best_change=_value(changes.max()) if len(changes) else None,
        worst_change=_value(changes.min()) if len(changes) else None,
        max_drawdown=float(drawdown(series).min()),
    )


def weights(positions: list[dict]) -> pd.Series:
    """Peso de cada ativo na carteira, só com posições positivas e identificadas (sem sigilo)."""
    frame = pd.DataFrame(positions)
    if frame.empty:
        return pd.Series(dtype=float)
    frame = frame[frame["assetId"].notna() & (frame["marketValue"].fillna(0) > 0)]
    values = frame.groupby("assetId")["marketValue"].sum().astype(float)
    total = values.sum()
    return (values / total).sort_values(ascending=False) if total else pd.Series(dtype=float)


def concentration(asset_weights: pd.Series) -> Concentration:
    """Quanto a carteira depende de poucos ativos. HHI vai de 0 (pulverizada) a 1 (um ativo só)."""
    if asset_weights.empty:
        return Concentration(0, None, None, None, None, None)
    ordered = asset_weights.sort_values(ascending=False)
    hhi = float((ordered**2).sum())
    return Concentration(
        positions=len(ordered),
        top1=float(ordered.iloc[:1].sum()),
        top5=float(ordered.iloc[:5].sum()),
        top10=float(ordered.iloc[:10].sum()),
        hhi=hhi,
        effective_count=1 / hhi if hhi else None,
    )


def concentration_curve(asset_weights: pd.Series) -> pd.DataFrame:
    """Participação acumulada: os N maiores ativos somam quanto da carteira."""
    ordered = asset_weights.sort_values(ascending=False).reset_index(drop=True)
    return pd.DataFrame({"ativos": range(1, len(ordered) + 1), "acumulado": ordered.cumsum().to_numpy()})


def overlap(first: pd.Series, second: pd.Series) -> tuple[int, float]:
    """Ativos em comum e a sobreposição: soma, ativo a ativo, do menor dos dois pesos (0 a 1)."""
    shared = first.index.intersection(second.index)
    return len(shared), float(pd.concat([first[shared], second[shared]], axis=1).min(axis=1).sum()) if len(shared) else 0.0


def by_issuer(positions: list[dict], net_worth: float | None, top: int = 10) -> pd.DataFrame:
    """Maiores emissores pelo valor somado das posições positivas. Posições sob sigilo ficam de fora."""
    frame = pd.DataFrame(positions)
    if frame.empty:
        return pd.DataFrame(columns=["emissor", "valor", "participação"])
    frame = frame[frame["assetId"].notna() & (frame["marketValue"].fillna(0) > 0)].copy()
    frame["emissor"] = frame["issuerName"].fillna("").str.strip().replace("", None).fillna(frame["assetDescription"]).fillna("Sem emissor")
    grouped = frame.groupby("emissor", as_index=False)["marketValue"].sum().rename(columns={"marketValue": "valor"})
    grouped["participação"] = grouped["valor"] / net_worth if net_worth else None
    return grouped.sort_values("valor", ascending=False).head(top).reset_index(drop=True)


def _value(number) -> float | None:
    return None if pd.isna(number) else float(number)
