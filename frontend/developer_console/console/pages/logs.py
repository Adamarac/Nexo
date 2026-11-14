"""Logs e eventos: busca no Loki por serviço, nível, texto, arquivo da CVM e correlação."""

import plotly.graph_objects as go
import streamlit as st

from console import data, theme, ui
from console.data import fetch
from console.sources.loki import LEVELS, SERVICES, LogFilter, build_query
from nexo_shared import formatting

PERIODS = {"15 min": 15, "1 h": 60, "6 h": 360, "24 h": 1440, "7 dias": 10080}
LIMITS = [100, 300, 1000]

# Os níveis usam as cores de estado; o nome do nível aparece junto na legenda e na tabela.
LEVEL_COLORS = {
    "Debug": "#a3abbd",
    "Information": theme.SERIES[0],
    "Warning": theme.SERIES[3],
    "Error": theme.FAIL,
    "Fatal": theme.INK,
}


def render() -> None:
    live = ui.page_header("Logs e eventos", "Busca nos logs da API e dos workers, guardados no Loki")
    with ui.card("filters"):
        log_filter, minutes, limit = _filters()
    ui.run(lambda: _body(log_filter, minutes, limit), live)


def _filters() -> tuple[LogFilter, int, int]:
    # Um link de outra página pode chegar com o arquivo ou a correlação já preenchidos.
    st.session_state.setdefault("logs-source", st.query_params.get("source", ""))
    st.session_state.setdefault("logs-correlation", st.query_params.get("correlation", ""))

    first = st.columns([1.3, 2.4, 1, 0.8], vertical_alignment="bottom")
    services = first[0].multiselect("Serviço", SERVICES, default=list(SERVICES), key="logs-services")
    levels = first[1].pills(
        "Nível", LEVELS, selection_mode="multi", default=["Information", "Warning", "Error", "Fatal"], key="logs-levels"
    )
    period = first[2].selectbox("Período", list(PERIODS), index=3, key="logs-period")
    limit = first[3].selectbox("Máximo", LIMITS, index=1, key="logs-limit")

    second = st.columns([2, 1.2, 1.2, 1])
    text = second[0].text_input("Texto contém", key="logs-text", placeholder="ex.: falha, cda_fi_2026", icon=":material/search:")
    source_key = second[1].text_input("SourceKey", key="logs-source", placeholder="cda_fi_202610")
    correlation = second[2].text_input("CorrelationId", key="logs-correlation")
    instance = second[3].text_input("Instância", key="logs-instance", placeholder="nome do container")

    log_filter = LogFilter(
        services=tuple(services),
        levels=tuple(levels or ()),
        text=text,
        source_key=source_key,
        correlation_id=correlation,
        instance=instance,
    )
    return log_filter, PERIODS[period], limit


def _body(log_filter: LogFilter, minutes: int, limit: int) -> None:
    if not log_filter.services:
        with ui.card("none"):
            ui.empty("Escolha ao menos um serviço.")
        return

    totals = fetch("loki", "totals", log_filter, minutes, "severity_text")
    by_level = dict(zip(totals.value["valor"], totals.value["logs"], strict=True)) if totals.ok else {}
    total = sum(by_level.values())
    ui.kpis(
        [
            ui.Kpi("Logs no período", formatting.number(total) if totals.ok else "—", "com os filtros acima"),
            ui.Kpi("Erros", formatting.number(by_level.get("Error", 0) + by_level.get("Fatal", 0)), "Error e Fatal"),
            ui.Kpi("Avisos", formatting.number(by_level.get("Warning", 0)), "Warning"),
            ui.Kpi("Informativos", formatting.number(by_level.get("Information", 0)), "Information"),
        ]
    )

    counts = fetch("loki", "counts", log_filter, minutes)
    events = fetch("loki", "totals", log_filter, minutes, "message_template_text")
    left, right = st.columns([3, 2], gap="medium")
    with left, ui.card("timeline", "Logs por nível ao longo do tempo", ui.sources(counts)):
        _timeline(counts)
    with right, ui.card("events", "Eventos mais frequentes", "pelo modelo da mensagem"):
        _top_events(events)

    logs = fetch("loki", "logs", log_filter, minutes, limit)
    with ui.card(
        "entries", f"Entradas · {len(logs.value) if logs.ok else 0} mais recentes", "clique numa linha para ver todos os atributos"
    ):
        _entries(logs)
    ui.note(f"Consulta LogQL: {build_query(log_filter)}")


def _timeline(counts: data.Reading) -> None:
    if not counts.ok:
        ui.unavailable(counts)
        return
    frame = counts.value
    if frame.empty:
        ui.empty("Nenhum log no período.")
        return
    figure = go.Figure()
    for level in LEVELS:
        points = frame[frame["nível"] == level]
        if not points.empty:
            figure.add_bar(x=ui.wall_clock(points["time"]), y=points["logs"], name=level, marker_color=LEVEL_COLORS[level])
    figure.update_layout(barmode="stack", bargap=0.15, hovermode="x unified")
    figure.update_yaxes(title_text="logs")
    ui.time_axis(figure)
    ui.chart(figure, 300)


def _top_events(events: data.Reading) -> None:
    if not events.ok:
        ui.unavailable(events)
        return
    frame = events.value[events.value["valor"] != "—"].head(8)
    if frame.empty:
        ui.empty("Nenhum evento no período.")
        return
    ui.bar_list(
        frame["valor"].tolist(),
        frame["logs"].tolist(),
        [formatting.number(value) for value in frame["logs"]],
        hover=[f"{template}<br>{formatting.number(count)} logs" for template, count in zip(frame["valor"], frame["logs"], strict=True)],
        colors=theme.ACCENT,
    )


def _entries(logs: data.Reading) -> None:
    if not logs.ok:
        ui.unavailable(logs)
        return
    frame = logs.value
    if frame.empty:
        ui.empty("Nenhum log com esses filtros no período.")
        return
    table = frame.assign(hora=ui.local_time(frame["time"]).to_numpy())
    columns = ["hora", "nível", "serviço", "instância", "mensagem", "SourceKey", "CorrelationId"]
    styled = table[columns].style.map(
        lambda value: f"color: {LEVEL_COLORS.get(value)}; font-weight: 600" if value in ("Error", "Fatal", "Warning") else "",
        subset=["nível"],
    )
    event = st.dataframe(
        styled,
        hide_index=True,
        width="stretch",
        height=440,
        on_select="rerun",
        selection_mode="single-row",
        key="logs-table",
        column_config={"hora": ui.time_column(seconds=True), "mensagem": st.column_config.TextColumn(width="large")},
    )
    if event.selection.rows:
        entry = frame.iloc[event.selection.rows[0]]
        st.code(entry["mensagem"], language=None, wrap_lines=True)
        attributes = {key: str(value) for key, value in entry["atributos"].items() if not key.startswith("exception")}
        ui.rows(attributes)
        stack = entry["atributos"].get("exception_stacktrace")
        if stack:
            st.caption("exceção")
            st.code(stack, language=None)
