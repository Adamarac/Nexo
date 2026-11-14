"""Fundos: busca no cadastro com filtros, escolha de colunas e exportação."""

import pandas as pd
import streamlit as st

from analytics import compare_list, data, theme, ui
from nexo_shared import formatting, labels

PAGE_SIZE = 50
PAGE_KEY = "funds-page"
ALL = "Todos"

SORTS = {"-netWorth": "Maior patrimônio", "netWorth": "Menor patrimônio", "name": "Nome (A–Z)", "-name": "Nome (Z–A)"}
WORTH = "Patrimônio no cadastro (R$)"
COLUMNS = ["Fundo", "CNPJ", "Tipo", "Situação", WORTH, "Última carteira"]
DEFAULT_COLUMNS = ["Fundo", "CNPJ", "Tipo", WORTH, "Última carteira"]


def render() -> None:
    ui.page_header("Fundos", "Busque no cadastro da CVM, filtre, escolha colunas e exporte", ["Fundos"])
    with ui.card("filters"):
        filters = _filters()

    result = data.funds(**filters, page=ui.current_page(PAGE_KEY), page_size=PAGE_SIZE)
    items = result["items"]
    by_type = pd.Series([item["fundType"] or "Sem tipo" for item in items]).value_counts()
    ui.kpis(
        [
            ui.Kpi("Fundos encontrados", formatting.number(result["totalItems"]), "com os filtros acima"),
            ui.Kpi("Nesta página", formatting.number(len(items)), f"página {ui.current_page(PAGE_KEY)}"),
            ui.Kpi("Tipo mais comum", by_type.index[0] if len(by_type) else formatting.MISSING, "nesta página"),
        ]
    )
    if not items:
        with ui.card("none"):
            ui.empty("Nenhum fundo com esses filtros.")
        return

    table = _table(items)
    left, right = st.columns([2, 1], gap="medium")
    with left, ui.card("list", "Resultado", "selecione uma linha para abrir a análise do fundo ou comparar"):
        event = st.dataframe(
            ui.tidy(table),
            hide_index=True,
            width="stretch",
            height=470,
            on_select="rerun",
            selection_mode="single-row",
            key="funds-table",
            column_order=st.session_state.get("funds-columns") or DEFAULT_COLUMNS,
            column_config={
                "Fundo": st.column_config.TextColumn(width="large"),
                WORTH: ui.money_column(help="Valor informado no cadastro da CVM; pode diferir do patrimônio da carteira"),
                "Última carteira": st.column_config.DateColumn(format="MM/YYYY"),
            },
        )
        ui.pager(result["totalItems"], PAGE_SIZE, PAGE_KEY)
        if event.selection.rows:
            _actions(table.iloc[event.selection.rows[0]])
    with right, ui.card("largest", "Maiores patrimônios desta página", "valor do cadastro"):
        top = table.dropna(subset=[WORTH]).nlargest(8, WORTH)
        if top.empty:
            ui.empty("Sem patrimônio informado.")
        else:
            ui.bar_list(
                top["Fundo"].tolist(),
                top[WORTH].tolist(),
                [formatting.money_compact(value) for value in top[WORTH]],
                hover=[f"{name}<br>{formatting.money(value)}" for name, value in zip(top["Fundo"], top[WORTH], strict=True)],
                colors=theme.ACCENT,
            )

    with ui.card("export", "Exportar", f"até {formatting.number(data.EXPORT_LIMIT)} fundos com os filtros acima"):
        _export(filters, result["totalItems"])


def _filters() -> dict:
    reset = ui.first_page(PAGE_KEY)
    search_column, type_column, situation_column, sort_column, columns_column = st.columns(
        [2.4, 1, 1.5, 1.3, 1], vertical_alignment="bottom"
    )
    search = search_column.text_input(
        "Busca", placeholder="Nome ou CNPJ do fundo", key="funds-search", on_change=reset, icon=":material/search:"
    )
    fund_type = type_column.selectbox("Tipo", [ALL, *labels.FUND_TYPES], key="funds-type", on_change=reset)
    situation = situation_column.selectbox("Situação", [ALL, *labels.FUND_SITUATIONS], key="funds-situation", on_change=reset)
    sort = sort_column.selectbox("Ordenar por", list(SORTS), format_func=SORTS.get, key="funds-sort", on_change=reset)
    with columns_column.popover("Colunas", width="stretch", icon=":material/view_column:"):
        st.multiselect("Colunas visíveis", COLUMNS, default=DEFAULT_COLUMNS, key="funds-columns")
    return {
        "search": search.strip() or None,
        "fund_type": None if fund_type == ALL else fund_type,
        "situation": None if situation == ALL else situation,
        "sort": sort,
    }


def _table(items: list[dict]) -> pd.DataFrame:
    return pd.DataFrame(
        {
            "id": [item["id"] for item in items],
            "Fundo": [item["name"] for item in items],
            "CNPJ": [formatting.cnpj(item["cnpj"]) for item in items],
            "Tipo": [item["fundType"] for item in items],
            "Situação": [item["situation"] for item in items],
            WORTH: [item["netWorth"] for item in items],
            "Última carteira": [formatting.parse_date(item["latestReferenceDate"]) for item in items],
        }
    )


def _actions(row: pd.Series) -> None:
    from analytics.navigation import PAGES

    fund_id = int(row["id"])
    ui.note(f"Selecionado: {row['Fundo']}")
    open_column, compare_column, _ = st.columns([1.3, 1.7, 1])
    if open_column.button("Analisar fundo", type="primary", width="stretch", icon=":material/insights:"):
        st.switch_page(PAGES["fund"], query_params={"fundo": str(fund_id)})
    added = compare_list.contains(fund_id)
    label = "Já está na comparação" if added else f"Comparar ({len(compare_list.ids())}/{compare_list.LIMIT})"
    if compare_column.button(label, width="stretch", disabled=added or compare_list.is_full(), icon=":material/compare_arrows:"):
        compare_list.add(fund_id)
        st.rerun()


def _export(filters: dict, total: int) -> None:
    if st.button(
        f"Preparar arquivo com {formatting.number(min(total, data.EXPORT_LIMIT))} fundos",
        key="funds-prepare",
        icon=":material/description:",
    ):
        rows = data.all_rows("search_funds", data.EXPORT_LIMIT, **filters)
        st.session_state["funds-export"] = (filters, _table(rows).drop(columns=["id"]))

    # O arquivo preparado só vale para os filtros com que foi montado.
    prepared = st.session_state.get("funds-export")
    if prepared is not None and prepared[0] == filters:
        ui.export_buttons(prepared[1], "fundos", "funds-export")
