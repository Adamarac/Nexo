"""Recursos: CPU e memória de cada processo .NET, e o consumo do RabbitMQ, do Redis e do PostgreSQL."""

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

from console import data, instances, theme, ui
from console.data import fetch
from nexo_shared import formatting, viz

WINDOW_MINUTES = 60

RUNTIME_QUERIES = {
    "heap": "sum by (instance) (dotnet_gc_last_collection_heap_size_bytes)",
    "collections": "sum by (instance) (increase(dotnet_gc_collections_total[1h]))",
    "pause": "sum by (instance) (increase(dotnet_gc_pause_time_seconds_total[1h]))",
    "exceptions": "sum by (instance) (increase(dotnet_exceptions_total[1h]))",
    "queue": "sum by (instance) (dotnet_thread_pool_queue_length)",
}


def render() -> None:
    live = ui.page_header("Recursos", "CPU e memória dos processos .NET e o consumo do RabbitMQ, Redis e PostgreSQL")
    ui.run(_body, live)


def _body() -> None:
    cpu = fetch("prometheus", "range", "sum by (instance) (rate(dotnet_process_cpu_time_seconds_total[2m])) * 100", WINDOW_MINUTES)
    memory = fetch("prometheus", "range", "dotnet_process_memory_working_set_bytes / 1048576", WINDOW_MINUTES)
    alive = instances.active()
    colors = theme.color_map(alive)

    left, right = st.columns(2, gap="medium")
    with left, ui.card("cpu", "CPU por container · última hora", ui.sources(cpu)):
        _lines(cpu, alive, colors, "% de um núcleo")
    with right, ui.card("memory", "Memória em uso · última hora", ui.sources(memory)):
        _lines(memory, alive, colors, "MB")

    rabbit, redis, postgres = fetch("rabbitmq", "nodes"), fetch("redis", "info"), fetch("database", "stats")
    left, middle, right = st.columns(3, gap="medium")
    with left, ui.card("rabbit", "RabbitMQ", ui.sources(rabbit)):
        _rabbitmq(rabbit)
    with middle, ui.card("redis", "Redis · cache das consultas", ui.sources(redis)):
        _redis(redis)
    with right, ui.card("postgres", "PostgreSQL", ui.sources(postgres)):
        _postgres(postgres)

    runtime = {name: fetch("prometheus", "instant", query) for name, query in RUNTIME_QUERIES.items()}
    left, right = st.columns([3, 2], gap="medium")
    with left, ui.card("runtime", "Runtime .NET por instância", ui.sources(runtime["heap"])):
        _runtime(runtime, alive)
    with right:
        connections = fetch("database", "connections")
        with ui.card("connections", "Conexões no PostgreSQL", ui.sources(connections)):
            _connections(connections)


def _lines(reading: data.Reading, alive: set[str], colors: dict, unit: str) -> None:
    if not reading.ok:
        ui.unavailable(reading)
        return
    frame = reading.value
    if frame.empty:
        ui.empty("Sem amostras na última hora.")
        return
    figure = go.Figure()
    for instance, points in frame[frame["instance"].isin(alive)].groupby("instance"):
        figure.add_scatter(
            x=ui.wall_clock(points["time"]), y=points["value"], name=instance, mode="lines", line=dict(color=colors.get(instance))
        )
    figure.update_yaxes(title_text=unit, rangemode="tozero")
    ui.time_axis(figure)
    figure.update_layout(hovermode="x unified")
    ui.chart(figure, 300)


def _ring(fraction: float | None, label: str, color: str) -> None:
    if fraction is None:
        return
    ui.chart(viz.ring(fraction, color, theme.TRACK, label), 150)


def _rabbitmq(reading: data.Reading) -> None:
    if not reading.ok:
        ui.unavailable(reading)
        return
    for node in reading.value:
        used, limit = node.get("mem_used", 0), node.get("mem_limit") or 0
        _ring(used / limit if limit else None, formatting.percent(used / limit if limit else None), theme.SERIES[0])
        ui.note("da memória que o RabbitMQ pode usar")
        ui.progress(
            [
                ui.Progress(
                    "Descritores de arquivo",
                    f"{formatting.number(node.get('fd_used', 0))} de {formatting.number(node.get('fd_total', 0))}",
                    node.get("fd_used", 0) / node["fd_total"] if node.get("fd_total") else None,
                    theme.SERIES[3],
                ),
                ui.Progress(
                    "Processos Erlang",
                    f"{formatting.number(node.get('proc_used'))} de {formatting.number(node.get('proc_total'))}",
                    node.get("proc_used", 0) / node["proc_total"] if node.get("proc_total") else None,
                    theme.SERIES[4],
                ),
            ]
        )
        ui.rows(
            {
                "Memória": f"{formatting.size(used)} de {formatting.size(limit)}",
                "Disco livre": formatting.size(node.get("disk_free")),
                "Alarme de disco abaixo de": formatting.size(node.get("disk_free_limit")),
                "No ar há": formatting.duration((node.get("uptime") or 0) / 1000),
            }
        )


def _redis(reading: data.Reading) -> None:
    if not reading.ok:
        ui.unavailable(reading)
        return
    info = reading.value
    lookups = info["hits"] + info["misses"]
    _ring(info["hits"] / lookups if lookups else None, formatting.percent(info["hits"] / lookups if lookups else None), theme.SERIES[2])
    ui.note(f"das buscas acharam a resposta no cache ({formatting.number(lookups)} desde o início)")
    ui.rows(
        {
            "Memória": f"{formatting.size(info['used_memory'])} (pico {formatting.size(info['peak_memory'])})",
            "Limite": formatting.size(info["max_memory"]) if info["max_memory"] else "sem limite",
            "Chaves": formatting.number(info["keys"]),
            "Época do cache": str(info["epoch"] or "—"),
            "Clientes": formatting.number(info["clients"]),
            "No ar há": formatting.duration(info["uptime_seconds"]),
        }
    )


def _postgres(reading: data.Reading) -> None:
    if not reading.ok:
        ui.unavailable(reading)
        return
    value = reading.value
    blocks = value["blks_hit"] + value["blks_read"]
    _ring(
        value["blks_hit"] / blocks if blocks else None, formatting.percent(value["blks_hit"] / blocks if blocks else None), theme.SERIES[4]
    )
    ui.note("das leituras acharam o bloco na memória")
    transactions = value["xact_commit"] + value["xact_rollback"]
    ui.progress(
        [
            ui.Progress(
                "Conexões",
                f"{value['numbackends']} de {value['max_connections']}",
                value["numbackends"] / value["max_connections"],
                theme.ACCENT,
            ),
        ]
    )
    ui.rows(
        {
            "Transações": formatting.compact(transactions),
            "Desfeitas": formatting.percent(value["xact_rollback"] / transactions if transactions else None, 2),
            "Arquivos temporários": f"{formatting.size(value['temp_bytes'])} em {formatting.number(value['temp_files'])}",
            "Deadlocks": formatting.number(value["deadlocks"]),
            "Estatísticas desde": formatting.moment(value["stats_reset"]) if pd.notna(value["stats_reset"]) else "a criação do banco",
        }
    )


def _runtime(readings: dict, alive: set[str]) -> None:
    first = readings["heap"]
    if not first.ok:
        ui.unavailable(first)
        return

    def column(name: str) -> dict:
        frame = readings[name].value if readings[name].ok else pd.DataFrame()
        return dict(zip(frame["instance"], frame["value"], strict=True)) if not frame.empty else {}

    heap, collections, pause, exceptions, queue = (column(name) for name in ("heap", "collections", "pause", "exceptions", "queue"))
    rows = [
        {
            "instância": instance,
            "heap após o GC": formatting.size(heap.get(instance)),
            "coletas 1 h": round(collections.get(instance, 0)),
            "pausa do GC 1 h": formatting.duration(pause.get(instance, 0)),
            "exceções 1 h": round(exceptions.get(instance, 0)),
            "fila do thread pool": int(queue.get(instance, 0)),
        }
        for instance in sorted(alive)
    ]
    if not rows:
        ui.empty("Nenhuma instância com sinal.")
        return
    st.dataframe(
        pd.DataFrame(rows),
        hide_index=True,
        width="stretch",
        column_config={"coletas 1 h": ui.count_column(), "exceções 1 h": ui.count_column()},
    )


def _connections(reading: data.Reading) -> None:
    if not reading.ok:
        ui.unavailable(reading)
        return
    frame = reading.value
    if frame.empty:
        ui.empty("Nenhuma conexão de cliente.")
        return
    labels_ = [f"{application} · {state}" for application, state in zip(frame["application"], frame["state"], strict=True)]
    ui.bar_list(labels_, frame["connections"].tolist(), [str(value) for value in frame["connections"]], colors=theme.SERIES[0])
    ui.note("Conexões sem nome vêm da API e dos workers (o Npgsql não informa o nome da aplicação).")
