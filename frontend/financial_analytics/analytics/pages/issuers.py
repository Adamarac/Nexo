"""Emissores: quem emitiu os títulos que os fundos têm, pelo CNPJ, e como cada fundo escreve o nome dele."""

import pandas as pd
import streamlit as st

from analytics import data, theme, ui
from nexo_shared import formatting

PAGE_SIZE = 50
SEARCH_PAGE = "issuers-page"
ALL = "Todas as formas"
SORTS = {
    "spellings": "Mais formas de escrever",
    "funds": "Mais fundos",
    "name": "Nome",
}


def render() -> None:
    document = _issuer_from_link()
    issuer = data.issuer(document) if document else None
    if issuer is None:
        _search(not_found=document is not None)
        return
    _detail(issuer)


def _issuer_from_link() -> str | None:
    value = st.query_params.get("emissor", "")
    digits = "".join(char for char in value if char.isdigit())
    return digits if len(digits) == 14 else None


def _search(not_found: bool) -> None:
    ui.page_header(
        "Emissores",
        "Quem emitiu os títulos das carteiras, identificado pelo CNPJ, e as formas como os fundos escrevem o nome",
        ["Emissores"],
    )
    _overview()

    with ui.card("filters"):
        if not_found:
            ui.note("O emissor do link não foi encontrado. Busque outro abaixo.")
        reset = ui.first_page(SEARCH_PAGE)
        search_column, sort_column = st.columns([3, 2], vertical_alignment="bottom")
        search = search_column.text_input(
            "Busca", placeholder="Parte do nome ou CNPJ", key="issuers-search", on_change=reset, icon=":material/search:"
        )
        sort = sort_column.selectbox("Ordem", list(SORTS), format_func=SORTS.get, key="issuers-sort", on_change=reset)
        only_varied = st.toggle("Só emissores escritos de mais de um jeito", key="issuers-varied", on_change=reset)

    result = data.issuers(
        search=search.strip() or None,
        min_spellings=2 if only_varied else None,
        sort=sort,
        page=ui.current_page(SEARCH_PAGE),
        page_size=PAGE_SIZE,
    )
    with ui.card("results", f"{formatting.number(result['totalItems'])} emissores", "selecione uma linha para ver as formas de escrever"):
        if not result["items"]:
            ui.empty("Nenhum emissor com essa busca.")
            return
        table = pd.DataFrame(
            [
                {
                    "document": item["document"],
                    "CNPJ": formatting.cnpj(item["document"]),
                    "Nome mais usado": item["name"],
                    "Formas de escrever": item["spellings"],
                    "Fundos": item["funds"],
                    "Último mês": formatting.month(item["lastReferenceDate"]),
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
            key="issuers-table",
            column_order=["Nome mais usado", "CNPJ", "Formas de escrever", "Fundos", "Último mês"],
            column_config={
                "Nome mais usado": st.column_config.TextColumn(width="large"),
                "Formas de escrever": ui.number_column(help="Quantos jeitos diferentes de escrever o nome aparecem nas carteiras"),
                "Fundos": ui.number_column(help="Fundos que tiveram papéis deste emissor em algum mês"),
            },
        )
        ui.pager(result["totalItems"], PAGE_SIZE, SEARCH_PAGE)
    if event.selection.rows:
        st.query_params["emissor"] = table.iloc[event.selection.rows[0]]["document"]
        st.rerun()


def _overview() -> None:
    everyone = data.issuers(page_size=1)
    varied = data.issuers(min_spellings=2, page_size=1)
    total, with_variants = everyone["totalItems"], varied["totalItems"]
    top = varied["items"][0] if varied["items"] else None
    ui.kpis(
        [
            ui.Kpi("Emissores", formatting.number(total), "com CNPJ, em todos os meses"),
            ui.Kpi(
                "Escritos de mais de um jeito",
                formatting.number(with_variants),
                f"{ui.percent(with_variants / total)} dos emissores" if total else "",
            ),
            ui.Kpi(
                "Mais formas de escrever",
                formatting.number(top["spellings"]) if top else "—",
                top["name"] if top else "",
            ),
        ]
    )


def _detail(issuer: dict) -> None:
    spellings = pd.DataFrame(issuer["spellings"])
    ui.page_header(issuer["name"], f"CNPJ {formatting.cnpj(issuer['document'])}", ["Emissores", "Detalhe"])
    if st.button("Voltar para a busca", icon=":material/arrow_back:"):
        st.query_params.pop("emissor", None)
        st.rerun()

    ui.kpis(
        [
            ui.Kpi("Formas de escrever", formatting.number(len(spellings)), "nomes diferentes nas carteiras"),
            ui.Kpi("Fundos", formatting.number(issuer["funds"]), "tiveram papéis deste emissor"),
            ui.Kpi("Primeiro mês", formatting.month(issuer["firstReferenceDate"]), "em que aparece"),
            ui.Kpi("Último mês", formatting.month(issuer["lastReferenceDate"]), "em que aparece"),
        ]
    )

    with ui.card("spellings", "Formas de escrever o nome", "quantos fundos usam cada uma; a primeira é a mais usada"):
        ui.bar_list(
            spellings["name"].tolist(),
            spellings["funds"].tolist(),
            [f"{formatting.number(value)} {'fundo' if value == 1 else 'fundos'}" for value in spellings["funds"]],
            hover=[
                f"{name}<br>de {formatting.month(first)} a {formatting.month(last)}"
                for name, first, last in zip(
                    spellings["name"], spellings["firstReferenceDate"], spellings["lastReferenceDate"], strict=True
                )
            ],
            colors=theme.ACCENT,
        )

    with ui.card("funds", "Como cada fundo escreve", "selecione um fundo para abrir a análise dele"):
        _fund_table(issuer, spellings["name"].tolist())


def _fund_table(issuer: dict, names: list[str]) -> None:
    chosen = ALL
    if len(names) > 1:
        chosen = st.selectbox("Forma de escrever", [ALL, *names], key=f"issuer-spelling-{issuer['document']}")
    funds = pd.DataFrame(data.all_issuer_funds(issuer["document"], spelling=None if chosen == ALL else chosen))
    if funds.empty:
        ui.empty("Nenhum fundo com essa forma de escrever.")
        return

    table = pd.DataFrame(
        {
            "fund_id": funds["fundId"],
            "Fundo": funds["fundName"],
            "CNPJ do fundo": funds["fundCnpj"].map(formatting.cnpj),
            "Como escreve": funds["spelling"],
            "Meses": funds["months"],
            "Primeiro mês": funds["firstReferenceDate"].map(formatting.month),
            "Último mês": funds["lastReferenceDate"].map(formatting.month),
        }
    )
    event = st.dataframe(
        ui.tidy(table),
        hide_index=True,
        width="stretch",
        height=min(40 + 35 * len(table), 560),
        on_select="rerun",
        selection_mode="single-row",
        key=f"issuer-funds-{issuer['document']}-{chosen}",
        column_order=["Como escreve", "Fundo", "CNPJ do fundo", "Meses", "Primeiro mês", "Último mês"],
        column_config={
            "Fundo": st.column_config.TextColumn(width="large"),
            "Como escreve": st.column_config.TextColumn(width="medium"),
            "Primeiro mês": st.column_config.TextColumn(width="small"),
            "Último mês": st.column_config.TextColumn(width="small"),
            "Meses": ui.number_column(help="Meses em que o fundo escreveu o nome deste jeito"),
        },
    )
    if event.selection.rows:
        from analytics.navigation import PAGES

        st.switch_page(PAGES["fund"], query_params={"fundo": str(int(table.iloc[event.selection.rows[0]]["fund_id"]))})
    ui.export_buttons(table.drop(columns=["fund_id"]), f"emissor-{issuer['document']}", "issuer-funds-export")
