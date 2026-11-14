"""Comparar: até quatro fundos lado a lado em medidas resumo, evolução, alocação e ativos em comum."""

from html import escape

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

from analytics import charts, compare_list, data, metrics, theme, ui
from nexo_shared import formatting, labels, viz

LETTERS = "ABCD"

# Escala de um tom só (azul), do claro ao escuro, para a matriz de sobreposição.
OVERLAP_SCALE = [[0, "#eef4fd"], [0.25, "#9ec5f4"], [0.5, "#5598e7"], [0.75, "#256abf"], [1, "#104281"]]


def render() -> None:
    ui.page_header("Comparar fundos", "Até quatro fundos lado a lado, na mesma data e na mesma régua", ["Comparar"])
    compare_list.load_from_link()
    with ui.card("chooser", "Fundos na comparação", f"até {compare_list.LIMIT}"):
        _chooser()
        compare_list.write_link()
        funds = [fund for fund in (data.fund(fund_id) for fund_id in compare_list.ids()) if fund]
        reference_date = _common_date(funds) if len(funds) >= 2 else None

    if len(funds) < 2:
        with ui.card("hint"):
            ui.empty("Escolha pelo menos dois fundos. Também dá para adicionar pelas páginas Fundos e Análise do fundo.")
        return

    colors = {fund["id"]: theme.COMPARE[index] for index, fund in enumerate(funds)}
    # Cada fundo vira uma letra (A, B, C, D) nos gráficos e tabelas: o nome inteiro fica na lista acima, sem cortes.
    names = {fund["id"]: f"Fundo {LETTERS[index]}" for index, fund in enumerate(funds)}
    portfolios = {fund["id"]: data.portfolio(fund["id"], reference_date=reference_date) for fund in funds}
    series = {fund["id"]: metrics.net_worth_series(data.history(fund["id"])) for fund in funds}
    positions = {fund_id: data.all_positions(portfolio["portfolioId"]) if portfolio else [] for fund_id, portfolio in portfolios.items()}
    weights = {fund_id: metrics.weights(items) for fund_id, items in positions.items()}

    with ui.card("measures", "Medidas resumo", "patrimônio até o mês escolhido; a variação inclui aplicações e resgates"):
        _measures(funds, portfolios, series, weights, names, colors, reference_date)

    left, right = st.columns(2, gap="medium")
    with left, ui.card("growth", "Patrimônio em base 100", "100 = primeiro mês em que todos têm dado"):
        _growth(funds, series, names, colors)
    with right, ui.card("spread", "Distribuição das variações mensais", "cada caixa resume os meses de um fundo"):
        _spread(funds, series, names, colors)

    with ui.card("allocation", "Alocação por tipo de aplicação", "% do patrimônio de cada fundo"):
        _allocation(funds, portfolios, names)

    left, right = st.columns([2, 3], gap="medium")
    with left, ui.card("overlap", "Sobreposição das carteiras", "soma do menor peso de cada ativo em comum"):
        _overlap_matrix(funds, weights, names)
    with right, ui.card("shared", "Ativos em comum", "escolha o par de fundos"):
        _shared_assets(funds, weights, positions, names)

    with ui.card("curves", "Curvas de concentração", "quanto os N maiores ativos somam de cada carteira"):
        _curves(funds, weights, names, colors)


def _chooser() -> None:
    for index, fund_id in enumerate(compare_list.ids()):
        fund = data.fund(fund_id)
        name = fund["name"] if fund else f"Fundo {fund_id}"
        label, action = st.columns([6, 1], vertical_alignment="center")
        label.html(
            f'<div style="display:flex;align-items:center;gap:.75rem;min-width:0;font-size:14px;line-height:1.45">'
            f'<span style="flex:none;width:10px;height:10px;border-radius:50%;background:{theme.COMPARE[index]}"></span>'
            f'<span style="flex:none;font-weight:600">Fundo {LETTERS[index]}</span>'
            f'<span style="overflow-wrap:anywhere">{escape(name)}</span></div>'
        )
        if action.button("Remover", key=f"remove-{fund_id}", width="stretch", icon=":material/close:"):
            compare_list.remove(fund_id)
            st.rerun()
    if not compare_list.is_full():
        picked = ui.fund_picker("compare", "Adicionar fundo")
        if picked and not compare_list.contains(picked):
            compare_list.add(picked)
            st.rerun()


def _common_date(funds: list[dict]) -> str | None:
    common = set(funds[0]["referenceDates"])
    for fund in funds[1:]:
        common &= set(fund["referenceDates"])
    if not common:
        ui.note("Os fundos não têm nenhum mês em comum; cada um aparece com a carteira mais recente.")
        return None
    return st.selectbox(
        "Mês de comparação",
        sorted(common, reverse=True),
        key="compare-date",
        format_func=lambda value: f"{formatting.month(value)} ({formatting.day(value)})",
    )


def _measures(funds, portfolios, series, weights, names, colors, reference_date) -> None:
    columns = {}
    for fund in funds:
        portfolio = portfolios[fund["id"]] or {}
        points = series[fund["id"]]
        if reference_date:
            points = points[points.index <= pd.Timestamp(reference_date)]
        summary = metrics.summarize(points)
        concentration = metrics.concentration(weights[fund["id"]])
        net_worth = portfolio.get("netWorth")
        confidential = portfolio.get("confidentialValue") or 0
        columns[names[fund["id"]]] = {
            "Patrimônio": formatting.money_compact(net_worth),
            "Posições": formatting.number(portfolio.get("positionCount")),
            "Variação no mês": ui.signed_percent(summary.change_month),
            "Variação em 12 meses": ui.signed_percent(summary.change_12m),
            "Variação mensal média": ui.signed_percent(summary.mean_change),
            "Desvio padrão mensal": ui.percent(summary.std_change),
            "Maior queda desde um pico": ui.signed_percent(summary.max_drawdown),
            "10 maiores ativos": ui.percent(concentration.top10),
            "Concentração (HHI)": formatting.number(concentration.hhi, 3) if concentration.hhi is not None else formatting.MISSING,
            "Sob sigilo": formatting.percent(confidential / net_worth) if net_worth else formatting.MISSING,
        }
    legend = " ".join(ui.pill(names[fund["id"]], colors[fund["id"]]) for fund in funds)
    st.html(f'<div style="display:flex;flex-wrap:wrap;gap:.4rem;margin:.2rem 0 .6rem">{legend}</div>')
    table = pd.DataFrame(columns).rename_axis("Medida").reset_index()
    st.dataframe(table, hide_index=True, width="stretch", height="content")
    ui.export_buttons(table, "comparacao-medidas", "compare-measures")


def _growth(funds, series, names, colors) -> None:
    starts = [points.index.min() for points in series.values() if not points.empty]
    if len(starts) < 2:
        ui.empty("Não há histórico suficiente para comparar.")
        return
    start = max(starts)
    indexed_by_fund = {}
    for fund in funds:
        points = series[fund["id"]]
        points = points[points.index >= start]
        if not points.empty:
            indexed_by_fund[fund["id"]] = (points, points / points.iloc[0] * 100)

    # Se um fundo cresceu muito mais que os outros, a escala logarítmica já começa ligada para nenhum ficar achatado.
    peaks = [indexed.max() for _, indexed in indexed_by_fund.values()]
    uneven = len(peaks) > 1 and max(peaks) / max(min(peaks), 1) > 4
    log_scale = st.toggle(
        "Escala logarítmica",
        value=uneven,
        key=f"compare-log-{uneven}",
        help="Distâncias iguais no eixo valem o mesmo crescimento percentual",
    )
    figure = go.Figure()
    for fund in funds:
        if fund["id"] not in indexed_by_fund:
            continue
        points, indexed = indexed_by_fund[fund["id"]]
        figure.add_scatter(
            x=indexed.index,
            y=indexed.values,
            name=names[fund["id"]],
            mode="lines",
            line=dict(color=colors[fund["id"]], width=2.5),
            customdata=points.map(formatting.money).values,
            hovertemplate="%{x|%m/%Y}<br>%{y:.1f} (%{customdata})<extra></extra>",
        )
    figure.add_hline(y=100, line_width=1, line_color=theme.TOKENS.line)
    figure.update_yaxes(title_text=f"base 100 = {formatting.month(start)}", type="log" if log_scale else "linear")
    if log_scale:
        figure.update_yaxes(dtick=1)
    figure.update_xaxes(tickformat="%m/%Y")
    figure.update_layout(hovermode="x unified")
    ui.chart(figure, 340)


def _spread(funds, series, names, colors) -> None:
    figure = go.Figure()
    pooled = []
    for fund in funds:
        changes = metrics.monthly_changes(series[fund["id"]]) * 100
        if changes.empty:
            continue
        pooled.extend(changes.tolist())
        figure.add_box(
            y=changes,
            name=names[fund["id"]],
            marker_color=colors[fund["id"]],
            boxpoints=False,
            boxmean=True,
            hovertemplate="%{y:.1f}%<extra></extra>",
        )
    if not figure.data:
        ui.empty("É preciso ao menos dois meses de carteira.")
        return
    # A escala cobre do 5º ao 95º percentil de todos os meses, para um mês extremo não achatar as caixas.
    values = pd.Series(pooled)
    low, high = values.quantile(0.05), values.quantile(0.95)
    padding = max((high - low) * 0.25, 1)
    outside = int(((values < low - padding) | (values > high + padding)).sum())
    figure.update_yaxes(ticksuffix="%", zeroline=True, zerolinecolor=theme.TOKENS.line, range=[low - padding, high + padding])
    figure.update_xaxes(showticklabels=False)
    figure.update_layout(showlegend=True)
    ui.chart(figure, 340)
    note = "A linha no meio é a mediana, o tracejado é a média e a caixa vai do 1º ao 3º quartil."
    if outside:
        note += f" {outside} meses extremos ficam fora da escala; veja-os na Análise do fundo."
    ui.note(note)


def _allocation(funds, portfolios, names) -> None:
    rows = [
        {"fundo": fund["id"], "tipo": item["applicationType"], "valor": item["marketValue"] or 0}
        for fund in funds
        for item in (portfolios[fund["id"]] or {}).get("allocation", [])
        if (item["marketValue"] or 0) > 0
    ]
    if not rows:
        ui.empty("Nenhum dos fundos tem carteira nesse mês.")
        return
    frame = pd.DataFrame(rows)
    order = charts.fold(frame, "tipo", "valor", keep=4)["tipo"].tolist()
    frame["tipo"] = frame["tipo"].where(frame["tipo"].isin(order), charts.OTHERS)
    totals = frame.groupby("fundo")["valor"].sum()
    shares = frame.groupby(["fundo", "tipo"])["valor"].sum().div(totals, level="fundo")
    fund_ids = [fund["id"] for fund in funds if fund["id"] in totals.index]
    matrix = {category: [float(shares.get((fund_id, category), 0.0)) for fund_id in fund_ids] for category in order}
    figure = viz.stacked_shares([names[fund_id] for fund_id in fund_ids], order, matrix, charts.category_colors(order), theme.TOKENS.card)
    ui.chart(figure, 90 + 56 * len(fund_ids) + 30 * ((len(order) + 2) // 3))
    with st.expander("Ver em tabela"):
        table = pd.DataFrame(
            {names[fund_id]: [matrix[category][index] for category in order] for index, fund_id in enumerate(fund_ids)}, index=order
        )
        table = table.rename_axis("Tipo de aplicação").reset_index()
        st.dataframe(table, hide_index=True, width="stretch", column_config={name: ui.share_column() for name in table.columns[1:]})


def _overlap_matrix(funds, weights, names) -> None:
    labels_ = [names[fund["id"]] for fund in funds]
    values, texts = [], []
    for first in funds:
        row, text = [], []
        for second in funds:
            if first["id"] == second["id"]:
                row.append(None)
                text.append("—")
                continue
            shared, overlap = metrics.overlap(weights[first["id"]], weights[second["id"]])
            row.append(overlap * 100)
            text.append(f"{formatting.percent(overlap)}<br>{shared} em comum")
        values.append(row)
        texts.append(text)

    short_labels = labels_
    figure = go.Figure(
        go.Heatmap(
            z=values,
            x=short_labels,
            y=short_labels,
            text=texts,
            texttemplate="%{text}",
            colorscale=OVERLAP_SCALE,
            zmin=0,
            zmax=100,
            showscale=False,
            hovertemplate="%{y} × %{x}<br>%{text}<extra></extra>",
            xgap=3,
            ygap=3,
            textfont=dict(size=12),
        )
    )
    figure.update_yaxes(autorange="reversed", showgrid=False)
    figure.update_xaxes(side="top", showgrid=False, tickangle=0, tickfont=dict(size=10))
    ui.chart(figure, 120 + 70 * len(funds))
    ui.note("0% = nenhum ativo em comum; 100% = carteiras idênticas. Posições sob sigilo ficam de fora.")


def _shared_assets(funds, weights, positions, names) -> None:
    pairs = [(first["id"], second["id"]) for index, first in enumerate(funds) for second in funds[index + 1 :]]
    pair = st.selectbox(
        "Par", pairs, format_func=lambda item: f"{names[item[0]]} × {names[item[1]]}", key="compare-pair", label_visibility="collapsed"
    )
    first, second = weights[pair[0]], weights[pair[1]]
    shared = first.index.intersection(second.index)
    if not len(shared):
        ui.empty("Esses dois fundos não têm ativos identificados em comum.")
        return
    # O ISIN fica numa coluna própria: muitos títulos têm a mesma descrição e só o código os diferencia.
    details = {}
    for item in positions[pair[0]] + positions[pair[1]]:
        if item["assetId"] is not None:
            details.setdefault(item["assetId"], (labels.asset(item["ticker"], item["assetDescription"]), item["isin"] or "—"))

    first_name, second_name = names[pair[0]], names[pair[1]]
    table = pd.DataFrame(
        {
            "Ativo": [details.get(asset, (str(asset), "—"))[0] for asset in shared],
            "ISIN": [details.get(asset, (str(asset), "—"))[1] for asset in shared],
            first_name: first[shared].to_numpy(),
            second_name: second[shared].to_numpy(),
        }
    )
    table["menor peso"] = table[[first_name, second_name]].min(axis=1)
    table = table.sort_values("menor peso", ascending=False).drop(columns=["menor peso"])
    st.dataframe(
        table,
        hide_index=True,
        width="stretch",
        height=min(40 + 35 * len(table), 330),
        column_config={
            first_name: ui.share_column(),
            second_name: ui.share_column(),
        },
    )
    ui.note(f"{formatting.number(len(shared))} ativos em comum; pesos sobre a parte identificada de cada carteira.")


def _curves(funds, weights, names, colors) -> None:
    figure = go.Figure()
    for fund in funds:
        curve = metrics.concentration_curve(weights[fund["id"]]).head(100)
        if curve.empty:
            continue
        figure.add_scatter(
            x=curve["ativos"],
            y=curve["acumulado"] * 100,
            name=names[fund["id"]],
            mode="lines",
            line=dict(color=colors[fund["id"]], width=2.5),
            hovertemplate="%{x} maiores ativos: %{y:.1f}%<extra></extra>",
        )
    if not figure.data:
        ui.empty("Nenhum ativo identificado nas carteiras.")
        return
    figure.update_yaxes(range=[0, 101], ticksuffix="%")
    figure.update_xaxes(title_text="número de ativos, do maior para o menor")
    ui.chart(figure, 320)
