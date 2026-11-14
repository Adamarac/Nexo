"""Análise do fundo: evolução do patrimônio, medidas resumo, alocação, concentração e posições."""

from html import escape

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

from analytics import charts, compare_list, data, metrics, theme, ui
from nexo_shared import formatting, labels, viz

PAGE_SIZE = 50
PAGE_KEY = "positions-page"
ALL = "Todos"
SORTS = {
    "-marketValue": "Maior valor",
    "marketValue": "Menor valor",
    "-share": "Maior % do patrimônio",
    "share": "Menor % do patrimônio",
    "applicationType": "Tipo de aplicação",
}
COLUMNS = [
    "Ativo",
    "Tipo de aplicação",
    "Tipo de ativo",
    "Grupo",
    "Emissor",
    "ISIN",
    "Ticker",
    "Quantidade",
    "Valor (R$)",
    "% do patrimônio",
    "Finalidade",
    "Sigilo até",
]
DEFAULT_COLUMNS = ["Ativo", "ISIN", "Tipo de aplicação", "Emissor", "Quantidade", "Valor (R$)", "% do patrimônio"]
FLOW_NOTE = "a variação do patrimônio inclui aplicações e resgates; não é rentabilidade"


def render() -> None:
    fund_id = _fund_from_link()
    fund = data.fund(fund_id) if fund_id else None
    if fund is None:
        _choose_fund(not_found=fund_id is not None)
        return

    _header(fund)
    if not fund["referenceDates"]:
        with ui.card("no-portfolio"):
            ui.empty("Este fundo ainda não informou nenhuma carteira à CVM.")
        return

    with ui.card("controls"):
        reference_date, class_id = _controls(fund)
    portfolio = data.portfolio(fund["id"], reference_date=reference_date, class_id=class_id)
    if portfolio is None:
        with ui.card("no-match"):
            ui.empty("Não há carteira para essa combinação de data e classe.")
        return

    series = metrics.net_worth_series(data.history(fund["id"], class_id=class_id))
    series = series[series.index <= pd.Timestamp(portfolio["referenceDate"])]
    summary = metrics.summarize(series)
    positions = data.all_positions(portfolio["portfolioId"])
    weights = metrics.weights(positions)
    concentration = metrics.concentration(weights)

    _indicators(portfolio, summary, concentration)

    left, right = st.columns([2, 1], gap="medium")
    with left, ui.card("evolution", "Evolução do patrimônio", "um ponto por mês informado"):
        _evolution(series)
    with right, ui.card("measures", "Medidas resumo", f"até {formatting.month(portfolio['referenceDate'])}"):
        _measures(summary, concentration)

    left, right = st.columns(2, gap="medium")
    with left, ui.card("allocation", "Alocação por tipo de aplicação", "% do patrimônio"):
        _allocation(portfolio)
    with right, ui.card("largest", "Maiores posições", "% do patrimônio"):
        _largest_positions(positions)

    left, right = st.columns(2, gap="medium")
    with left, ui.card("changes", "Variação mensal do patrimônio", "últimos 24 meses"):
        _monthly_changes(series)
    with right, ui.card("curve", "Curva de concentração", "quanto os N maiores ativos somam da carteira"):
        _concentration_curve(weights)

    left, right = st.columns([2, 1], gap="medium")
    with left, ui.card("issuers", "Maiores emissores", "valor somado das posições identificadas"):
        _issuers(positions, portfolio["netWorth"])
    with right, ui.card("dependence", "Dependência dos maiores ativos", "participação na parte identificada"):
        _dependence(concentration)

    with ui.card("positions", "Posições da carteira", "cada linha é um ativo informado"):
        _positions(portfolio)


def _fund_from_link() -> int | None:
    value = st.query_params.get("fundo", "")
    return int(value) if value.isdigit() else None


def _choose_fund(not_found: bool) -> None:
    ui.page_header("Análise do fundo", "Escolha um fundo para ver a evolução, as medidas resumo e a carteira", ["Análise do fundo"])
    with ui.card("pick", "Qual fundo?"):
        if not_found:
            ui.note("O fundo do link não foi encontrado. Busque outro abaixo.")
        chosen = ui.fund_picker("fund")
    if chosen:
        _open(chosen)


def _open(fund_id: int) -> None:
    st.query_params.clear()
    st.query_params["fundo"] = str(fund_id)
    st.session_state.pop(PAGE_KEY, None)
    st.rerun()


def _header(fund: dict) -> None:
    tags = [ui.pill(fund["fundType"], theme.ACCENT)] if fund["fundType"] else []
    if fund["situation"]:
        tags.append(ui.pill(fund["situation"], theme.SERIES[2] if fund["situation"] == "Em Funcionamento Normal" else theme.NEGATIVE))
    if fund["isPlaceholder"]:
        tags.append(ui.pill("cadastro provisório", theme.OTHER))
    subtitle = f"CNPJ {escape(formatting.cnpj(fund['cnpj']))} &nbsp; " + " ".join(tags)
    ui.page_header(fund["name"], subtitle, ["Análise do fundo"])


def _controls(fund: dict) -> tuple[str, int | None]:
    dates = sorted(fund["referenceDates"], reverse=True)
    classes = {item["id"]: item for item in fund["classes"]}
    date_column, class_column, compare_column, change_column = st.columns([1.2, 2.2, 1.3, 1.2], vertical_alignment="bottom")
    reference_date = date_column.selectbox(
        "Mês de referência",
        dates,
        format_func=lambda value: f"{formatting.month(value)} ({formatting.day(value)})",
        key=f"fund-date-{fund['id']}",
        on_change=ui.first_page(PAGE_KEY),
    )
    class_id = None
    if len(classes) > 1:
        class_id = class_column.selectbox(
            "Classe",
            [None, *classes],
            key=f"fund-class-{fund['id']}",
            on_change=ui.first_page(PAGE_KEY),
            format_func=lambda value: "Classe de maior patrimônio no mês" if value is None else classes[value]["name"],
        )
    else:
        class_column.html(
            f'<div class="kit-note">Administrador: <b>{escape(fund["administratorName"] or "—")}</b><br>'
            f"Gestor: <b>{escape(fund['managerName'] or '—')}</b></div>"
        )
    added = compare_list.contains(fund["id"])
    if compare_column.button(
        "Na comparação" if added else "Comparar",
        width="stretch",
        disabled=added or compare_list.is_full(),
        icon=":material/compare_arrows:",
    ):
        compare_list.add(fund["id"])
        st.rerun()
    with change_column.popover("Trocar fundo", width="stretch", icon=":material/swap_horiz:"):
        chosen = ui.fund_picker("fund-change", "Outro fundo")
        if chosen:
            _open(chosen)
    return reference_date, class_id


def _indicators(portfolio: dict, summary: metrics.NetWorthSummary, concentration: metrics.Concentration) -> None:
    net_worth = portfolio["netWorth"]
    confidential = portfolio["confidentialValue"] or 0
    ui.kpis(
        [
            ui.Kpi(
                "Patrimônio",
                formatting.money_compact(net_worth),
                formatting.money(net_worth),
            ),
            ui.Kpi("Variação no mês", ui.signed_percent(summary.change_month), "inclui aplicações e resgates"),
            ui.Kpi(
                "Variação em 12 meses",
                ui.signed_percent(summary.change_12m),
                "mesmo mês do ano anterior",
            ),
            ui.Kpi(
                "Posições",
                formatting.number(portfolio["positionCount"]),
                f"revisão {portfolio['revision']}",
            ),
            ui.Kpi("10 maiores ativos", ui.percent(concentration.top10), "da parte identificada"),
            ui.Kpi(
                "Sob sigilo",
                formatting.money_compact(confidential) if confidential else "Nada",
                f"{formatting.percent(confidential / net_worth)} do patrimônio" if confidential and net_worth else "carteira toda aberta",
            ),
        ]
    )


def _evolution(series: pd.Series) -> None:
    if series.empty:
        ui.empty("Sem histórico de patrimônio.")
        return
    divisor, unit = ui.money_scale(series.max())
    figure = go.Figure(
        go.Scatter(
            x=series.index,
            y=series / divisor,
            mode="lines+markers",
            line=dict(color=theme.INK, width=1.75),
            marker=dict(size=6),
            customdata=series.map(formatting.money),
            hovertemplate="%{x|%m/%Y}<br>%{customdata}<extra></extra>",
        )
    )
    figure.update_yaxes(title_text=unit, rangemode="tozero")
    figure.update_xaxes(tickformat="%m/%Y")
    figure.update_layout(hovermode="x unified", showlegend=False)
    ui.chart(figure, 320)
    ui.note(f"Atenção: {FLOW_NOTE}.")


def _measures(summary: metrics.NetWorthSummary, concentration: metrics.Concentration) -> None:
    ui.rows(
        {
            "Meses com carteira": formatting.number(summary.months),
            "Desde": formatting.month(summary.first_month),
            "Variação no período": ui.signed_percent(summary.change_total),
            "Variação mensal média": ui.signed_percent(summary.mean_change),
            "Desvio padrão mensal": ui.percent(summary.std_change),
            "Melhor mês": ui.signed_percent(summary.best_change),
            "Pior mês": ui.signed_percent(summary.worst_change),
            "Maior queda desde um pico": ui.signed_percent(summary.max_drawdown),
            "Ativos identificados": formatting.number(concentration.positions),
            "Índice de concentração (HHI)": formatting.number(concentration.hhi, 3)
            if concentration.hhi is not None
            else formatting.MISSING,
            "Equivale a ativos de mesmo peso": formatting.number(concentration.effective_count, 1)
            if concentration.effective_count
            else formatting.MISSING,
        }
    )


def _allocation(portfolio: dict) -> None:
    slices = pd.DataFrame(portfolio["allocation"])
    if slices.empty:
        ui.empty("A carteira não tem posições.")
        return
    slices["nome"] = [
        f"{name} (sigilo)" if hidden else name for name, hidden in zip(slices["applicationType"], slices["isConfidential"], strict=True)
    ]
    positive = slices[slices["marketValue"].fillna(0) > 0]
    grouped = charts.fold(positive, "nome", "marketValue", keep=4)
    colors = charts.category_colors(grouped["nome"].tolist())
    net_worth = portfolio["netWorth"] or grouped["marketValue"].sum()
    ui.donut(
        grouped["nome"].tolist(),
        grouped["marketValue"].tolist(),
        [colors[name] for name in grouped["nome"]],
        [f"{formatting.percent(value / net_worth)} · {formatting.money_compact(value)}" for value in grouped["marketValue"]],
        formatting.percent(grouped["marketValue"].iloc[0] / net_worth),
    )
    ui.note(f"No meio, a parte do maior tipo: {grouped['nome'].iloc[0]}.")
    with st.expander("Ver em tabela"):
        st.dataframe(
            pd.DataFrame(
                {
                    "Tipo de aplicação": slices["nome"],
                    "Posições": slices["positions"],
                    "Valor (R$)": slices["marketValue"],
                    "% do PL": slices["shareOfNetWorth"],
                }
            ),
            hide_index=True,
            width="stretch",
            column_config={"Valor (R$)": ui.money_column(), "% do PL": ui.share_column(), "Posições": ui.number_column()},
        )


def _largest_positions(positions: list[dict]) -> None:
    frame = pd.DataFrame(positions)
    if frame.empty:
        ui.empty("A carteira não tem posições.")
        return
    top = frame.dropna(subset=["shareOfNetWorth"]).nlargest(10, "shareOfNetWorth")
    names = viz.distinct([labels.asset(row.ticker, row.assetDescription) for row in top.itertuples()], top["isin"].tolist())
    ui.bar_list(
        names,
        (top["shareOfNetWorth"] * 100).tolist(),
        [formatting.percent(share) for share in top["shareOfNetWorth"]],
        hover=[f"{name}<br>{formatting.money(value)}" for name, value in zip(names, top["marketValue"], strict=True)],
        colors=theme.ACCENT,
    )


def _monthly_changes(series: pd.Series) -> None:
    changes = metrics.monthly_changes(series).tail(24)
    if changes.empty:
        ui.empty("É preciso ao menos dois meses de carteira.")
        return
    colors = [theme.ACCENT if value >= 0 else theme.NEGATIVE for value in changes]
    figure = go.Figure(
        go.Bar(
            x=changes.index,
            y=changes * 100,
            marker_color=colors,
            customdata=[ui.signed_percent(value) for value in changes],
            hovertemplate="%{x|%m/%Y}<br>%{customdata}<extra></extra>",
        )
    )
    figure.update_yaxes(ticksuffix="%", zeroline=True, zerolinecolor=theme.TOKENS.line)
    figure.update_xaxes(tickformat="%m/%Y")
    figure.update_layout(bargap=0.25)
    ui.chart(figure, 280)
    ui.note("Azul: patrimônio subiu; laranja: caiu. " + FLOW_NOTE.capitalize() + ".")


def _concentration_curve(weights: pd.Series) -> None:
    curve = metrics.concentration_curve(weights)
    if curve.empty:
        ui.empty("Nenhum ativo identificado nesta carteira.")
        return
    curve = curve.head(100)
    figure = go.Figure(
        go.Scatter(
            x=curve["ativos"],
            y=curve["acumulado"] * 100,
            mode="lines",
            line=dict(color=theme.INK, width=1.75),
            hovertemplate="%{x} maiores ativos<br>%{y:.1f}% da carteira<extra></extra>",
        )
    )
    figure.update_yaxes(range=[0, 101], ticksuffix="%")
    figure.update_xaxes(title_text="número de ativos, do maior para o menor")
    figure.update_layout(showlegend=False)
    ui.chart(figure, 280)


def _issuers(positions: list[dict], net_worth) -> None:
    issuers = metrics.by_issuer(positions, net_worth, top=10)
    if issuers.empty:
        ui.empty("Nenhuma posição identificada.")
        return
    texts = [
        formatting.percent(share) if pd.notna(share) else formatting.money_compact(value)
        for share, value in zip(issuers["participação"], issuers["valor"], strict=True)
    ]
    ui.bar_list(
        issuers["emissor"].tolist(),
        issuers["valor"].tolist(),
        texts,
        hover=[f"{name}<br>{formatting.money(value)}" for name, value in zip(issuers["emissor"], issuers["valor"], strict=True)],
        colors=theme.SERIES[2],
    )


def _dependence(concentration: metrics.Concentration) -> None:
    if concentration.positions == 0:
        ui.empty("Nenhum ativo identificado.")
        return
    ui.progress(
        [
            ui.Progress("Maior ativo", ui.percent(concentration.top1), concentration.top1, theme.SERIES[0]),
            ui.Progress("5 maiores ativos", ui.percent(concentration.top5), concentration.top5, theme.SERIES[1]),
            ui.Progress("10 maiores ativos", ui.percent(concentration.top10), concentration.top10, theme.SERIES[3]),
            ui.Progress("Concentração (HHI)", formatting.number(concentration.hhi, 3), concentration.hhi, theme.INK),
        ]
    )
    ui.note("HHI perto de 0 indica carteira pulverizada; perto de 1, um ativo só.")


def _positions(portfolio: dict) -> None:
    filters = _position_filters()
    result = data.positions(portfolio["portfolioId"], **filters, page=ui.current_page(PAGE_KEY), page_size=PAGE_SIZE)
    if not result["items"]:
        ui.empty("Nenhuma posição com esses filtros.")
        return
    table = _position_table(result["items"])
    st.dataframe(
        ui.tidy(table),
        hide_index=True,
        width="stretch",
        height=min(40 + 35 * len(table), 520),
        column_order=st.session_state.get("positions-columns") or DEFAULT_COLUMNS,
        column_config={
            "Ativo": st.column_config.TextColumn(width="large"),
            "Quantidade": ui.number_column(),
            "Valor (R$)": ui.money_column(),
            "% do patrimônio": ui.share_column(),
            "Sigilo até": st.column_config.DateColumn(format="DD/MM/YYYY"),
        },
    )
    ui.pager(result["totalItems"], PAGE_SIZE, PAGE_KEY)

    key = (portfolio["portfolioId"], tuple(sorted(filters.items())))
    if st.button(
        f"Preparar exportação das {formatting.number(result['totalItems'])} posições",
        key="positions-prepare",
        icon=":material/description:",
    ):
        rows = data.all_rows("positions", data.EXPORT_LIMIT, portfolio["portfolioId"], **filters)
        st.session_state["positions-export"] = (key, _position_table(rows))
    prepared = st.session_state.get("positions-export")
    if prepared is not None and prepared[0] == key:
        ui.export_buttons(prepared[1], f"carteira-{portfolio['fundId']}-{portfolio['referenceDate']}", "positions-export")


def _position_filters() -> dict:
    reset = ui.first_page(PAGE_KEY)
    search_column, block_column, sort_column, columns_column = st.columns([2.4, 2, 1.5, 1], vertical_alignment="bottom")
    search = search_column.text_input(
        "Busca", placeholder="Ativo, emissor, ISIN ou ticker", key="positions-search", on_change=reset, icon=":material/search:"
    )
    block = block_column.selectbox(
        "Grupo da CVM",
        [ALL, *labels.BLOCKS],
        key="positions-block",
        on_change=reset,
        format_func=lambda value: "Todos os grupos" if value == ALL else labels.block(value),
    )
    sort = sort_column.selectbox("Ordenar por", list(SORTS), format_func=SORTS.get, key="positions-sort", on_change=reset)
    with columns_column.popover("Colunas", width="stretch", icon=":material/view_column:"):
        st.multiselect("Colunas visíveis", COLUMNS, default=DEFAULT_COLUMNS, key="positions-columns")
    return {"search": search.strip() or None, "block": None if block == ALL else block, "sort": sort}


def _position_table(items: list[dict]) -> pd.DataFrame:
    return pd.DataFrame(
        {
            "Ativo": [labels.asset(item["ticker"], item["assetDescription"]) for item in items],
            "Tipo de aplicação": [item["applicationType"] for item in items],
            "Tipo de ativo": [item["assetType"] for item in items],
            "Grupo": [labels.block(item["block"]) for item in items],
            "Emissor": [item["issuerName"] for item in items],
            "ISIN": [item["isin"] for item in items],
            "Ticker": [item["ticker"] for item in items],
            "Quantidade": [item["quantity"] for item in items],
            "Valor (R$)": [item["marketValue"] for item in items],
            "% do patrimônio": [item["shareOfNetWorth"] for item in items],
            "Finalidade": [item["tradingIntent"] for item in items],
            "Sigilo até": [formatting.parse_date(item["confidentialUntil"]) for item in items],
        }
    )
