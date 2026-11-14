"""Peças visuais da Análise: o kit comum com as cores daqui, mais tabelas, paginação e exportação."""

import math
from datetime import date

import pandas as pd
import streamlit as st

from analytics import data, theme
from nexo_shared import export, formatting, ui_kit, viz
from nexo_shared.ui_kit import Kpi, Progress, card, chart, empty, note, pill, rows

__all__ = ["Kpi", "Progress", "card", "chart", "empty", "note", "pill", "rows"]


def apply_style() -> None:
    ui_kit.apply_style(theme.TOKENS)


def page_header(title: str, subtitle: str = "", crumbs: list[str] | None = None) -> None:
    ui_kit.page_header(title, subtitle, ["Análise", *(crumbs or [])])


def kpis(items: list[Kpi]) -> None:
    ui_kit.kpis(items, theme.TOKENS)


def progress(items: list[Progress]) -> None:
    ui_kit.progress(items, theme.TOKENS)


def bar_list(labels, values, texts, hover=None, colors=None) -> None:
    ui_kit.bar_list(labels, values, texts, theme.TOKENS, hover=hover, colors=colors)


def legend(labels, colors, texts) -> None:
    ui_kit.legend(labels, colors, texts)


def unavailable(error: Exception) -> None:
    with card("unavailable", "Não foi possível consultar os dados agora"):
        note("Tente de novo em alguns instantes. Se continuar, avise a equipe responsável pelo Nexo.")
        with st.expander("detalhe técnico"):
            st.code(str(error), language=None)


def tidy(frame: pd.DataFrame) -> pd.DataFrame:
    """Prepara a tabela para leitura: texto vazio vira '—' e números ou datas guardados como objeto viram o tipo certo."""
    result = frame.copy()
    for column in result.columns:
        values = result[column]
        present = values.dropna()
        if present.empty:
            continue
        # O pandas 3 guarda texto num tipo próprio; vazios nele também viram '—'.
        if pd.api.types.is_string_dtype(values.dtype) and values.dtype != object:
            result[column] = values.fillna(formatting.MISSING)
        elif values.dtype != object:
            continue
        elif present.map(lambda value: isinstance(value, int | float) and not isinstance(value, bool)).all():
            result[column] = pd.to_numeric(values)
        elif present.map(lambda value: isinstance(value, date)).all():
            result[column] = pd.to_datetime(values)
        elif present.map(lambda value: isinstance(value, str)).all():
            result[column] = values.fillna(formatting.MISSING)
    return result


def money_scale(largest: float) -> tuple[float, str]:
    """Escolhe a unidade do eixo pelo maior valor: R$ bi, R$ mi ou R$ mil, para o eixo não virar 'G' e 'M'."""
    for divisor, unit in [(1e12, "R$ tri"), (1e9, "R$ bi"), (1e6, "R$ mi"), (1e3, "R$ mil")]:
        if abs(largest) >= divisor:
            return divisor, unit
    return 1.0, "R$"


def number_column(label: str | None = None, help: str | None = None):
    return st.column_config.NumberColumn(label, format="localized", help=help)


def money_column(label: str | None = None, help: str | None = None):
    return st.column_config.NumberColumn(label, format="localized", help=help)


def share_column(label: str | None = None):
    return st.column_config.NumberColumn(label, format="percent")


def current_page(key: str) -> int:
    return st.session_state.get(key, 1)


def first_page(key: str):
    """Callback para os filtros: quando a busca muda, a tabela volta para a primeira página."""
    return lambda: st.session_state.update({key: 1})


def pager(total: int, page_size: int, key: str) -> None:
    """Seletor de página embaixo da tabela; o valor fica na sessão e vale na próxima execução."""
    pages = max(1, math.ceil(total / page_size))
    if pages > 1:
        left, right = st.columns([1, 4], vertical_alignment="center")
        left.number_input("página", min_value=1, max_value=pages, step=1, key=key, label_visibility="collapsed")
        right.caption(f"de {formatting.number(pages)} páginas · {formatting.number(total)} linhas")


def export_buttons(frame: pd.DataFrame, file_name: str, key: str) -> None:
    left, right, _ = st.columns([1, 1, 3])
    left.download_button(
        "CSV", export.to_csv(frame), f"{file_name}.csv", "text/csv", key=f"{key}-csv", width="stretch", icon=":material/download:"
    )
    right.download_button(
        "Excel",
        export.to_excel(frame),
        f"{file_name}.xlsx",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        key=f"{key}-xlsx",
        width="stretch",
        icon=":material/table_view:",
    )


def fund_picker(key: str, label: str = "Fundo") -> int | None:
    """Busca de fundo por nome ou CNPJ, com a lista dos mais relevantes para escolher."""
    search = st.text_input(label, key=f"{key}-search", placeholder="Digite parte do nome ou o CNPJ", icon=":material/search:")
    if len(search.strip()) < 3:
        return None
    result = data.funds(search=search.strip(), sort="-netWorth", page_size=20)
    options = {item["id"]: item for item in result["items"]}
    if not options:
        empty("Nenhum fundo encontrado com esse nome ou CNPJ.")
        return None
    return st.selectbox(
        "Resultado",
        list(options),
        key=f"{key}-choice",
        index=None,
        placeholder=f"{formatting.number(result['totalItems'])} fundos encontrados; escolha um",
        format_func=lambda fund_id: f"{options[fund_id]['name']} · {formatting.cnpj(options[fund_id]['cnpj'])}",
    )


def percent(value) -> str:
    return formatting.percent(value) if value is not None else formatting.MISSING


def signed_percent(value) -> str:
    if value is None:
        return formatting.MISSING
    return ("+" if value > 0 else "") + formatting.percent(value)


def donut(labels, values, colors, texts, center: str) -> None:
    """Rosca com o número principal no meio e a legenda em lista embaixo, como no design de referência."""
    ui_kit.chart(viz.donut(list(values), list(colors), center, theme.TOKENS.card), 220)
    ui_kit.legend(labels, colors, texts)
