"""Workers: quais instâncias estão vivas, o que cada uma faz e quanto tempo os arquivos levam."""

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

from console import data, instances, theme, ui
from console.data import fetch
from console.sources.rabbitmq import PROCESS_QUEUE, connection_table, consumers_by_host
from nexo_shared import formatting

INSTANCE_QUERIES = {
    "memory": "dotnet_process_memory_working_set_bytes",
    "cpu": "sum by (job, instance) (rate(dotnet_process_cpu_time_seconds_total[2m]))",
    "threads": "dotnet_thread_pool_thread_count",
}

# Totais que cada instância acumula enquanto está no ar; zeram quando o container reinicia.
PROCESSING_TOTALS = {
    "files": "nexo_processing_duration_seconds_count",
    "rows": "nexo_processing_rows_read_total",
    "seconds": "nexo_processing_duration_seconds_sum",
    "failures": "nexo_processing_failures_total",
}
CARDS_PER_ROW = 3


def render() -> None:
    live = ui.page_header("Workers", "Instâncias da API, do watcher e dos workers, e o tempo de cada arquivo")
    ui.run(_body, live)


def _body() -> None:
    readings = {name: fetch("prometheus", "instant", query) for name, query in INSTANCE_QUERIES.items()}
    consumers = fetch("rabbitmq", "consumers")
    connections = fetch("rabbitmq", "connections")
    processing = fetch("database", "processing_files")
    durations = fetch("database", "file_durations", 60)
    table = _instance_table(readings, consumers, connections)

    _indicators(table, processing, durations)
    if table is None:
        with ui.card("down", "Instâncias"):
            ui.unavailable(readings["memory"])
    else:
        _instance_cards(table[table["estado"] == "OK"])
        stale = table[table["estado"] != "OK"]
        if not stale.empty:
            with st.expander(
                f"{len(stale)} instâncias sem sinal há mais de {instances.STALE_AFTER_SECONDS} s (containers parados ou recriados)"
            ):
                st.dataframe(ui.status_style(stale.drop(columns=["cpu"]), "estado"), hide_index=True, width="stretch")

    left, right = st.columns(2, gap="medium")
    with left:
        totals = {name: fetch("prometheus", "instant", f"sum by (instance) ({metric})") for name, metric in PROCESSING_TOTALS.items()}
        with ui.card("totals", "Processado por instância", "desde que o container subiu"):
            _processing_totals(totals)
    with right, ui.card("now", "Em processamento agora", ui.sources(processing)):
        _processing_now(processing)

    with ui.card("durations", "Tempo de processamento por arquivo", ui.sources(durations)):
        _durations(durations)

    if connections.ok and connections.value:
        with st.expander("Conexões com o RabbitMQ"):
            frame = connection_table(connections.value)
            frame["aberta desde"] = ui.local_time(frame["aberta desde"])
            st.dataframe(frame, hide_index=True, width="stretch", column_config={"aberta desde": ui.time_column()})


def _instance_table(readings: dict, consumers: data.Reading, connections: data.Reading) -> pd.DataFrame | None:
    if not readings["memory"].ok or readings["memory"].value.empty:
        return None

    def by_instance(name: str) -> dict:
        frame = readings[name].value if readings[name].ok else pd.DataFrame()
        return dict(zip(frame["instance"], frame["value"], strict=True)) if not frame.empty else {}

    hosts = consumers_by_host(consumers.value, connections.value) if consumers.ok and connections.ok else {}
    cpu, threads, ages = by_instance("cpu"), by_instance("threads"), instances.ages()
    rows = []
    for _, series in readings["memory"].value.sort_values(["job", "instance"]).iterrows():
        instance = series["instance"]
        queues = sorted({consumer["fila"] for consumer in hosts.get(instance, [])})
        seconds = ages.get(instance)
        rows.append(
            {
                "instância": instance,
                "serviço": series["job"],
                "papel": _role(series["job"], instance, queues),
                "estado": "OK" if seconds is not None and seconds <= instances.STALE_AFTER_SECONDS else "SEM SINAL",
                "último sinal": formatting.duration(seconds) if seconds is not None else formatting.MISSING,
                "consome": ", ".join(queues) or "—",
                "memória": formatting.size(series["value"]),
                "cpu": cpu.get(instance, 0.0),
                "threads": int(threads.get(instance, 0)),
            }
        )
    return pd.DataFrame(rows)


def _role(job: str, instance: str, queues: list[str]) -> str:
    if job == "nexo-api":
        return "API"
    if PROCESS_QUEUE in queues:
        return "Worker"
    if "watcher" in instance:
        return "Watcher"
    return "—"


def _indicators(table, processing: data.Reading, durations: data.Reading) -> None:
    alive = table[table["estado"] == "OK"] if table is not None else pd.DataFrame()
    workers = int((alive["papel"] == "Worker").sum()) if not alive.empty else 0
    seconds = durations.value["seconds"].astype(float) if durations.ok and not durations.value.empty else pd.Series(dtype=float)
    ui.kpis(
        [
            ui.Kpi("Instâncias com sinal", formatting.number(len(alive)), "processos .NET mandando métricas"),
            ui.Kpi("Workers", formatting.number(workers), f"consumindo {PROCESS_QUEUE}"),
            ui.Kpi(
                "Processando agora",
                formatting.number(len(processing.value)) if processing.ok else "—",
                "arquivos com reserva",
            ),
            ui.Kpi(
                "Tempo mediano",
                formatting.duration(seconds.median()) if len(seconds) else "—",
                "por arquivo da CVM",
            ),
            ui.Kpi(
                "Mais lento",
                formatting.duration(seconds.max()) if len(seconds) else "—",
                "entre os últimos 60",
            ),
        ]
    )


def _instance_cards(alive: pd.DataFrame) -> None:
    if alive.empty:
        with ui.card("none", "Instâncias"):
            ui.empty("Nenhuma instância mandou métricas no último minuto.")
        return
    records = alive.to_dict("records")
    for start in range(0, len(records), CARDS_PER_ROW):
        columns = st.columns(CARDS_PER_ROW, gap="medium")
        for column, record in zip(columns, records[start : start + CARDS_PER_ROW], strict=False):
            with column, ui.card(f"instance-{record['instância']}", record["instância"], record["serviço"]):
                st.html(
                    f'<div style="display:flex;gap:.4rem;flex-wrap:wrap;margin:.2rem 0 .5rem">{ui.state(record["estado"])}'
                    f"{ui.pill(record['papel'], theme.INK)}</div>"
                )
                ui.progress([ui.Progress("CPU (um núcleo)", formatting.percent(record["cpu"]), record["cpu"], theme.ACCENT)])
                ui.rows(
                    {
                        "Memória em uso": record["memória"],
                        "Threads do pool": str(record["threads"]),
                        "Consome": record["consome"],
                        "Último sinal": f"há {record['último sinal']}",
                    }
                )


def _processing_totals(totals: dict) -> None:
    files = totals["files"]
    if not files.ok:
        ui.unavailable(files)
        return
    if files.value.empty:
        ui.empty("Nenhuma instância processou arquivo desde que subiu.")
        return

    def column(name: str) -> dict:
        frame = totals[name].value if totals[name].ok else pd.DataFrame()
        return dict(zip(frame["instance"], frame["value"], strict=True)) if not frame.empty else {}

    rows_read, seconds, failures = column("rows"), column("seconds"), column("failures")
    frame = files.value.sort_values("value", ascending=False)
    ui.bar_list(
        frame["instance"].tolist(),
        frame["value"].tolist(),
        [
            f"{int(count)} {'arquivo' if int(count) == 1 else 'arquivos'} · {formatting.compact(rows_read.get(instance, 0))} linhas"
            for instance, count in zip(frame["instance"], frame["value"], strict=True)
        ],
        hover=[
            f"{instance}<br>{formatting.duration(seconds.get(instance, 0))} somados · {int(failures.get(instance, 0))} falhas"
            for instance in frame["instance"]
        ],
        colors=[theme.color_map(frame["instance"])[instance] for instance in frame["instance"]],
    )


def _processing_now(processing: data.Reading) -> None:
    if not processing.ok:
        ui.unavailable(processing)
        return
    if processing.value.empty:
        ui.empty("Nenhum arquivo em processamento.")
        return
    ui.rows(
        {
            row["source_key"]: f"desde {formatting.moment(row['processing_started_at'], seconds=True)[-8:]} · tentativa {row['attempts']}"
            for _, row in processing.value.iterrows()
        }
    )


def _durations(durations: data.Reading) -> None:
    if not durations.ok:
        ui.unavailable(durations)
        return
    if durations.value.empty:
        ui.empty("Nenhum arquivo processado ainda.")
        return
    frame = durations.value.sort_values("source_key")
    seconds = frame["seconds"].astype(float)
    median = seconds.median()
    figure = go.Figure(
        go.Bar(
            x=frame["source_key"].map(ui.short_key),
            y=seconds,
            marker_color=[theme.SERIES[2] if value > median * 1.5 else theme.SERIES[0] for value in seconds],
            customdata=list(zip(frame["rows_read"].map(formatting.number), seconds.map(formatting.duration), strict=True)),
            hovertext=frame["source_key"],
            hovertemplate="%{hovertext}<br>%{customdata[1]} · %{customdata[0]} linhas<extra></extra>",
        )
    )
    figure.add_hline(
        y=median,
        line_dash="dot",
        line_color=theme.INK,
        annotation_text=f"mediana {formatting.duration(median)}",
        annotation_position="top right",
        annotation_font=dict(size=11, color=theme.INK),
        annotation_bgcolor="rgba(255,255,255,0.9)",
    )
    figure.update_yaxes(title_text="segundos", rangemode="tozero")
    figure.update_xaxes(type="category", nticks=12, tickangle=0, tickfont=dict(size=10))
    figure.update_layout(bargap=0.2)
    ui.chart(figure, 300)
    ui.note("Em laranja, arquivos que levaram mais de 1,5 vez a mediana.")
