"""Ativos: busca de ativos e, para cada um, quais fundos têm, quanto e com que peso."""

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

from analytics import data, theme, ui
from nexo_shared import formatting, labels

PAGE_SIZE = 50
SEARCH_PAGE = "assets-page"
HOLDERS_PAGE = "holders-page"
TOP_HOLDERS = 12
ALL = "Todos"


def render() -> None:
    asset_id = _asset_from_link()
    asset = data.asset(asset_id) if asset_id else None
    if asset is None:
        _search(not_found=asset_id is not None)
        return
    _detail(asset)


def _asset_from_link() -> int | None:
    value = st.query_params.get("ativo", "")
    return int(value) if value.isdigit() else None


def _search(not_found: bool) -> None:
    ui.page_header("Ativos", "Títulos, ações, cotas e outros ativos que aparecem nas carteiras dos fundos", ["Ativos"])
    with ui.card("filters"):
        if not_found:
            ui.note("O ativo do link não foi encontrado. Busque outro abaixo.")
        reset = ui.first_page(SEARCH_PAGE)
        search_column, block_column = st.columns([3, 2], vertical_alignment="bottom")
        search = search_column.text_input(
            "Busca", placeholder="Descrição, emissor, ISIN ou ticker", key="assets-search", on_change=reset, icon=":material/search:"
        )
        block = block_column.selectbox(
            "Grupo da CVM",
            [ALL, *labels.BLOCKS],
            key="assets-block",
            on_change=reset,
            format_func=lambda value: "Todos os grupos" if value == ALL else labels.block(value),
        )
    if len(search.strip()) < 2 and block == ALL:
        with ui.card("hint"):
            ui.empty("Digite ao menos duas letras ou escolha um grupo para começar.")
        return

    result = data.assets(
        search=search.strip() or None, block=None if block == ALL else block, page=ui.current_page(SEARCH_PAGE), page_size=PAGE_SIZE
    )
    with ui.card(
        "results", f"{formatting.number(result['totalItems'])} ativos encontrados", "selecione uma linha para ver quem tem o ativo"
    ):
        if not result["items"]:
            ui.empty("Nenhum ativo com essa busca.")
            return
        table = pd.DataFrame(
            [
                {
                    "id": item["id"],
                    "Ativo": labels.asset(item["ticker"], item["description"], item["maturityDate"]),
                    "Tipo de aplicação": item["applicationType"],
                    "Tipo de ativo": item["assetType"],
                    "Emissor": item["issuerName"],
                    "ISIN": item["isin"],
                    "Vencimento": formatting.parse_date(item["maturityDate"]),
                }
                for item in result["items"]
            ]
        )
        event = st.dataframe(
            ui.tidy(table),
            hide_index=True,
            width="stretch",
            height=min(40 + 35 * len(table), 560),
            on_select="rerun",
            selection_mode="single-row",
            key="assets-table",
            column_order=["Ativo", "Tipo de aplicação", "Tipo de ativo", "Emissor", "ISIN", "Vencimento"],
            column_config={
                "Ativo": st.column_config.TextColumn(width="large"),
                "Vencimento": st.column_config.DateColumn(format="DD/MM/YYYY"),
            },
        )
        ui.pager(result["totalItems"], PAGE_SIZE, SEARCH_PAGE)
    if event.selection.rows:
        st.query_params["ativo"] = str(int(table.iloc[event.selection.rows[0]]["id"]))
        st.session_state.pop(HOLDERS_PAGE, None)
        st.rerun()


def _detail(asset: dict) -> None:
    name = labels.asset(asset["ticker"], asset["description"], asset["maturityDate"])
    tags = " ".join(
        ui.pill(text, color)
        for text, color in [
            (asset["applicationType"], theme.ACCENT),
            (asset["assetType"], theme.SERIES[2]),
            (labels.block(asset["block"]), theme.SERIES[4]),
        ]
        if text
    )
    ui.page_header(name, tags, ["Ativos", "Detalhe"])
    if st.button("Voltar para a busca", icon=":material/arrow_back:"):
        st.query_params.pop("ativo", None)
        st.rerun()

    reference = data.holders_date(asset["id"])
    holders = pd.DataFrame(data.all_holders(asset["id"], reference)) if reference else pd.DataFrame()
    total = holders["marketValue"].sum() if not holders.empty else 0
    ui.kpis(
        [
            ui.Kpi(
                "Fundos que têm",
                formatting.number(len(holders)),
                f"em {formatting.month(reference)}" if reference else "nas carteiras atuais",
            ),
            ui.Kpi(
                "Valor somado",
                formatting.money_compact(total) if total else "—",
                "nas carteiras desses fundos",
            ),
            ui.Kpi(
                "Maior posição",
                formatting.money_compact(holders["marketValue"].max()) if total else "—",
                holders.iloc[0]["fundName"] if total else "",
            ),
            ui.Kpi(
                "Peso mediano",
                ui.percent(holders["shareOfNetWorth"].median()) if not holders.empty else "—",
                "no patrimônio de quem tem",
            ),
        ]
    )

    left, right = st.columns([3, 2], gap="medium")
    with left, ui.card("facts", "Ficha do ativo"):
        ui.rows(
            {
                "Descrição": asset["description"] or formatting.MISSING,
                "Emissor": asset["issuerName"] or formatting.MISSING,
                "ISIN": asset["isin"] or formatting.MISSING,
                "Ticker": asset["ticker"] or formatting.MISSING,
                "Vencimento": formatting.day(asset["maturityDate"]),
                "País": asset["countryCode"] or formatting.MISSING,
            }
        )
    with right, ui.card("share", "Quanto os maiores detentores concentram", "parte do valor somado"):
        _holder_concentration(holders, total)

    if holders.empty:
        with ui.card("none"):
            ui.empty("Nenhum fundo tem este ativo nas carteiras atuais.")
        return

    left, right = st.columns(2, gap="medium")
    with left, ui.card("top", f"Os {TOP_HOLDERS} maiores detentores", "valor da posição"):
        top = holders.nlargest(TOP_HOLDERS, "marketValue")
        ui.bar_list(
            top["fundName"].tolist(),
            top["marketValue"].tolist(),
            [formatting.money_compact(value) for value in top["marketValue"]],
            hover=[
                f"{name}<br>{formatting.money(value)} · {ui.percent(share)} do patrimônio"
                for name, value, share in zip(top["fundName"], top["marketValue"], top["shareOfNetWorth"], strict=True)
            ],
            colors=theme.ACCENT,
        )
    with right, ui.card("weights", "Peso do ativo na carteira de quem tem", "quantos fundos em cada faixa"):
        _weight_histogram(holders)

    with ui.card("holders", "Fundos que têm este ativo", "selecione um fundo para abrir a análise dele"):
        _holder_table(asset, holders, reference)


def _holder_concentration(holders: pd.DataFrame, total: float) -> None:
    if holders.empty or not total:
        ui.empty("Sem detentores.")
        return
    ordered = holders["marketValue"].sort_values(ascending=False)
    ui.progress(
        [
            ui.Progress("Maior detentor", ui.percent(ordered.iloc[:1].sum() / total), ordered.iloc[:1].sum() / total, theme.SERIES[0]),
            ui.Progress("5 maiores", ui.percent(ordered.iloc[:5].sum() / total), ordered.iloc[:5].sum() / total, theme.SERIES[1]),
            ui.Progress("10 maiores", ui.percent(ordered.iloc[:10].sum() / total), ordered.iloc[:10].sum() / total, theme.SERIES[3]),
        ]
    )


def _weight_histogram(holders: pd.DataFrame) -> None:
    weights = holders["shareOfNetWorth"].dropna() * 100
    if weights.empty:
        ui.empty("Sem pesos informados.")
        return
    edges = [0, 1, 2, 5, 10, 20, 50, 100.0001]
    names = ["até 1%", "1–2%", "2–5%", "5–10%", "10–20%", "20–50%", "mais de 50%"]
    counts = pd.cut(weights.clip(upper=100), bins=edges, labels=names, right=False).value_counts().reindex(names, fill_value=0)
    figure = go.Figure(
        go.Bar(
            x=names,
            y=counts.values,
            marker_color=theme.ACCENT,
            text=[formatting.number(value) for value in counts.values],
            textposition="outside",
            cliponaxis=False,
            hovertemplate="%{x}: %{y} fundos<extra></extra>",
        )
    )
    figure.update_yaxes(title_text="fundos", range=[0, max(counts.max() * 1.2, 1)])
    ui.chart(figure, 300)


def _holder_table(asset: dict, holders: pd.DataFrame, reference) -> None:
    table = pd.DataFrame(
        {
            "fund_id": holders["fundId"],
            "Fundo": holders["fundName"],
            "CNPJ": holders["fundCnpj"].map(formatting.cnpj),
            "Quantidade": holders["quantity"],
            "Valor (R$)": holders["marketValue"],
            "% do patrimônio do fundo": holders["shareOfNetWorth"],
        }
    )
    event = st.dataframe(
        ui.tidy(table),
        hide_index=True,
        width="stretch",
        height=min(40 + 35 * len(table), 460),
        on_select="rerun",
        selection_mode="single-row",
        key="holders-table",
        column_order=["Fundo", "CNPJ", "Quantidade", "Valor (R$)", "% do patrimônio do fundo"],
        column_config={
            "Fundo": st.column_config.TextColumn(width="large"),
            "Quantidade": ui.number_column(),
            "Valor (R$)": ui.money_column(),
            "% do patrimônio do fundo": ui.share_column(),
        },
    )
    if event.selection.rows:
        from analytics.navigation import PAGES

        st.switch_page(PAGES["fund"], query_params={"fundo": str(int(table.iloc[event.selection.rows[0]]["fund_id"]))})
    ui.export_buttons(table.drop(columns=["fund_id"]), f"detentores-{asset['id']}-{reference}", "holders-export")
