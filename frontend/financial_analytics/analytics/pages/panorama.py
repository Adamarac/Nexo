"""Panorama: o tamanho da base e onde está o dinheiro dos fundos no mês mais recente."""

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

from analytics import charts, data, theme, ui
from nexo_shared import formatting, labels, viz

TOP_ASSETS = 100


def render() -> None:
    summary = data.dashboard()
    reference = summary["latestReferenceDate"]
    ui.page_header(
        "Panorama dos fundos",
        f"Carteiras mensais informadas à CVM · mês mais recente <b>{formatting.month(reference)}</b> · "
        f"atualizado em {formatting.moment(summary['lastProcessedAt'])}",
        ["Panorama"],
    )
    with_portfolio = summary["fundsWithPortfolio"] / summary["funds"] if summary["funds"] else None
    ui.kpis(
        [
            ui.Kpi("Fundos cadastrados", formatting.number(summary["funds"]), "cadastro da CVM"),
            ui.Kpi("Com carteira", formatting.number(summary["fundsWithPortfolio"]), f"{ui.percent(with_portfolio)} do cadastro"),
            ui.Kpi("Ativos distintos", formatting.compact(summary["assets"]), "títulos, ações, cotas e outros"),
            ui.Kpi("Carteiras", formatting.compact(summary["portfolios"]), "somando todos os meses"),
        ]
    )

    report = data.concentration(top=TOP_ASSETS)
    assets = _asset_frame(report["items"])

    left, right = st.columns(2, gap="medium")
    with left, ui.card("types", "Fundos com carteira, por tipo", "tipo do cadastro da CVM"):
        _fund_types(summary["fundsByType"])
    with right, ui.card("money", "Onde está o dinheiro", f"{TOP_ASSETS} ativos mais investidos · {formatting.month(reference)}"):
        _money_by_type(assets)

    left, right = st.columns(2, gap="medium")
    with left, ui.card("top", "Os 10 ativos com mais dinheiro dos fundos", formatting.month(reference)):
        _top_assets(assets.head(10))
    with right, ui.card("spread", "Valor investido × número de fundos", "cada ponto é um ativo"):
        _value_vs_funds(assets)

    with ui.card("table", f"Os {TOP_ASSETS} ativos mais investidos", "selecione uma linha para ver quem tem o ativo"):
        _asset_table(assets, reference)


def _asset_frame(items: list[dict]) -> pd.DataFrame:
    frame = pd.DataFrame(items)
    if frame.empty:
        return frame
    frame["ativo"] = [labels.asset(row.ticker, row.description, row.maturityDate) for row in frame.itertuples()]
    return frame


def _fund_types(by_type: list[dict]) -> None:
    if not by_type:
        ui.empty("Nenhum fundo com carteira ainda.")
        return
    frame = pd.DataFrame(by_type).sort_values("count", ascending=False)
    total = frame["count"].sum()
    ui.bar_list(
        frame["label"].tolist(),
        frame["count"].tolist(),
        [f"{formatting.number(count)} · {formatting.percent(count / total)}" for count in frame["count"]],
        colors=theme.ACCENT,
    )


def _money_by_type(assets: pd.DataFrame) -> None:
    if assets.empty:
        ui.empty("Ainda não há carteiras para esse mês.")
        return
    grouped = charts.fold(assets, "applicationType", "totalMarketValue", keep=4)
    colors = charts.category_colors(grouped["applicationType"].tolist())
    total = grouped["totalMarketValue"].sum()
    ui.donut(
        grouped["applicationType"].tolist(),
        grouped["totalMarketValue"].tolist(),
        [colors[name] for name in grouped["applicationType"]],
        [f"{formatting.money_compact(value)} · {formatting.percent(value / total)}" for value in grouped["totalMarketValue"]],
        formatting.money_compact(total),
    )


def _top_assets(top: pd.DataFrame) -> None:
    if top.empty:
        ui.empty("Ainda não há carteiras para esse mês.")
        return
    # Títulos com a mesma descrição se diferenciam pelo vencimento, que fica depois do ponto e não é cortado.
    names = viz.distinct(
        [labels.asset(row.ticker, row.description) for row in top.itertuples()],
        [f"venc. {formatting.month(row.maturityDate)}" if isinstance(row.maturityDate, str) else row.isin for row in top.itertuples()],
    )
    ui.bar_list(
        names,
        top["totalMarketValue"].tolist(),
        [formatting.money_compact(value) for value in top["totalMarketValue"]],
        hover=[
            f"{name}<br>{formatting.money(value)} em {formatting.number(funds)} fundos"
            for name, value, funds in zip(top["ativo"], top["totalMarketValue"], top["funds"], strict=True)
        ],
        colors=theme.ACCENT,
    )


def _value_vs_funds(assets: pd.DataFrame) -> None:
    if assets.empty:
        ui.empty("Ainda não há carteiras para esse mês.")
        return
    groups = charts.fold(assets, "applicationType", "totalMarketValue", keep=5)["applicationType"].tolist()
    colors = charts.category_colors(groups)
    assets = assets.assign(grupo=assets["applicationType"].where(assets["applicationType"].isin(groups), charts.OTHERS))
    figure = go.Figure()
    for group in groups:
        points = assets[assets["grupo"] == group]
        figure.add_scatter(
            x=points["funds"],
            y=points["totalMarketValue"] / 1e9,
            mode="markers",
            name=group,
            marker=dict(size=10, color=colors[group], line=dict(width=1, color="#ffffff"), opacity=0.9),
            customdata=list(zip(points["ativo"], points["totalMarketValue"].map(formatting.money_compact), strict=True)),
            hovertemplate="%{customdata[0]}<br>%{customdata[1]} · %{x} fundos<extra></extra>",
        )
    figure.update_xaxes(type="log", dtick=1, title_text="fundos que têm o ativo (escala log)")
    figure.update_yaxes(title_text="R$ bi")
    figure.update_layout(hovermode="closest", legend=dict(y=-0.3))
    ui.chart(figure, 360)


def _asset_table(assets: pd.DataFrame, reference) -> None:
    if assets.empty:
        ui.empty("Ainda não há carteiras para esse mês.")
        return
    table = pd.DataFrame(
        {
            "Ativo": assets["ativo"],
            "Tipo de aplicação": assets["applicationType"],
            "ISIN": assets["isin"],
            "Fundos que têm": assets["funds"],
            "Valor total (R$)": assets["totalMarketValue"],
            "id": assets["assetId"],
        }
    )
    event = st.dataframe(
        ui.tidy(table),
        hide_index=True,
        width="stretch",
        height=380,
        on_select="rerun",
        selection_mode="single-row",
        key="panorama-assets",
        column_order=["Ativo", "Tipo de aplicação", "ISIN", "Fundos que têm", "Valor total (R$)"],
        column_config={
            "Ativo": st.column_config.TextColumn(width="large"),
            "Fundos que têm": ui.number_column(),
            "Valor total (R$)": ui.money_column(),
        },
    )
    if event.selection.rows:
        from analytics.navigation import PAGES

        st.switch_page(PAGES["assets"], query_params={"ativo": str(int(table.iloc[event.selection.rows[0]]["id"]))})
    ui.export_buttons(table.drop(columns=["id"]), f"ativos-mais-investidos-{reference}", "panorama-export")
