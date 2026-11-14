"""Visão geral: os serviços estão de pé, o pipeline anda e há erros recentes?"""

import plotly.graph_objects as go
import streamlit as st

from console import data, instances, theme, ui
from console.data import fetch
from console.sources.loki import LEVELS, LogFilter
from console.sources.rabbitmq import PROCESS_QUEUE, queue_history
from nexo_shared import formatting, labels

STATUS_COLORS = {
    "Processed": theme.OK,
    "Downloaded": theme.SERIES[0],
    "Processing": theme.WARNING,
    "Failed": theme.FAIL,
    "Superseded": theme.MUTED,
}
LEVEL_COLORS = {
    "Debug": "#a3abbd",
    "Information": theme.SERIES[0],
    "Warning": theme.SERIES[3],
    "Error": theme.FAIL,
    "Fatal": theme.INK,
}


def render() -> None:
    live = ui.page_header("Visão geral", "O pipeline está saudável? Se não está, onde está o problema?")
    ui.run(_body, live)


def _body() -> None:
    status = fetch("api", "ingestion_status")
    queues = fetch("rabbitmq", "queues")
    consumers = fetch("rabbitmq", "consumers")
    _indicators(status, queues, consumers)

    left, right = st.columns([3, 2], gap="medium")
    with left, ui.card("services", "Saúde dos serviços", "lido agora em cada fonte"):
        _services(consumers)
    with right, ui.card("statuses", "Arquivos por situação", ui.sources(status)):
        _statuses(status)

    left, right = st.columns([3, 2], gap="medium")
    with left, ui.card("queue-history", "Mensagens nas filas · última hora", ui.sources(queues)):
        _queue_history(queues)
    with right:
        levels = fetch("loki", "totals", LogFilter(), 24 * 60, "severity_text")
        with ui.card("levels", "Logs por nível · 24 h", ui.sources(levels)):
            _levels(levels)

    errors = fetch("loki", "logs", LogFilter(levels=("Error", "Fatal")), 24 * 60, 8)
    with ui.card("errors", "Erros recentes · 24 h", ui.sources(errors)):
        _errors(errors)


def _indicators(status: data.Reading, queues: data.Reading, consumers: data.Reading) -> None:
    by_status = status.value["filesByStatus"] if status.ok else {}
    waiting = sum(queue.get("messages", 0) for queue in queues.value) if queues.ok else None
    consuming = sum(1 for item in consumers.value if item.get("queue", {}).get("name") == PROCESS_QUEUE) if consumers.ok else None
    failed = by_status.get("Failed", 0)
    ui.kpis(
        [
            ui.Kpi(
                "Processados",
                formatting.number(by_status.get("Processed", 0)) if status.ok else "—",
                "arquivos da CVM",
            ),
            ui.Kpi(
                "Aguardando",
                formatting.number(by_status.get("Downloaded", 0)) if status.ok else "—",
                "baixados, sem processar",
            ),
            ui.Kpi(
                "Falharam",
                formatting.number(failed) if status.ok else "—",
                "precisam de atenção" if failed else "nenhum arquivo",
            ),
            ui.Kpi(
                "Na fila",
                formatting.number(waiting) if waiting is not None else "—",
                "mensagens no RabbitMQ",
            ),
            ui.Kpi(
                "Workers",
                formatting.number(consuming) if consuming is not None else "—",
                f"consumindo {PROCESS_QUEUE}",
            ),
            ui.Kpi(
                "Último processamento",
                formatting.ago(status.value["lastProcessedAt"]) if status.ok else "—",
                formatting.moment(status.value["lastProcessedAt"]) if status.ok else "",
            ),
        ]
    )


def _services(consumers: data.Reading) -> None:
    checks = {
        "API": fetch("api", "health"),
        "PostgreSQL": fetch("database", "stats"),
        "RabbitMQ": fetch("rabbitmq", "overview"),
        "Redis": fetch("redis", "info"),
        "Prometheus": fetch("prometheus", "ready"),
        "Loki": fetch("loki", "ready"),
    }
    items = []
    for name, reading in checks.items():
        if not reading.ok:
            items.append((name, "FALHA", reading.error or ""))
        elif reading.value is False:
            items.append((name, "FALHA", "respondeu, mas diz que não está pronto"))
        else:
            items.append((name, "OK", _detail(name, reading.value)))
    alive = len(instances.active())
    if consumers.ok:
        count = sum(1 for item in consumers.value if item.get("queue", {}).get("name") == PROCESS_QUEUE)
        items.append(("Workers", "OK" if count else "ATENÇÃO", f"{count} consumindo a fila · {alive} instâncias .NET com sinal"))
    else:
        items.append(("Workers", "FALHA", consumers.error or ""))
    ui.services(items)


def _detail(name: str, value) -> str:
    if name == "PostgreSQL":
        return f"{str(value['server_version']).split(' on ')[0]} · {value['numbackends']} de {value['max_connections']} conexões"
    if name == "RabbitMQ":
        return f"versão {value.get('rabbitmq_version')}"
    if name == "Redis":
        return f"versão {value['version']} · {value['keys']} chaves"
    if name == "API":
        return "/health respondeu 200"
    return "pronto para consultas"


def _statuses(status: data.Reading) -> None:
    if not status.ok:
        ui.unavailable(status)
        return
    counts = {key: value for key, value in status.value["filesByStatus"].items() if value}
    if not counts:
        ui.empty("Nenhum arquivo baixado ainda.")
        return
    total = sum(counts.values())
    ordered = sorted(counts.items(), key=lambda item: item[1], reverse=True)
    ui.bar_list(
        [labels.file_status(key) for key, _ in ordered],
        [value for _, value in ordered],
        [f"{value} · {formatting.percent(value / total)}" for _, value in ordered],
        colors=[STATUS_COLORS.get(key, theme.MUTED) for key, _ in ordered],
    )
    ui.progress(
        [
            ui.Progress(
                "Concluídos", f"{formatting.percent(counts.get('Processed', 0) / total)}", counts.get("Processed", 0) / total, theme.OK
            )
        ]
    )


def _queue_history(queues: data.Reading) -> None:
    if not queues.ok:
        ui.unavailable(queues)
        return
    frame = queue_history(queues.value)
    if frame.empty:
        ui.empty("O RabbitMQ ainda não tem amostras de histórico.")
        return
    colors = theme.color_map(frame["fila"])
    figure = go.Figure()
    for name, points in frame.groupby("fila"):
        figure.add_scatter(
            x=ui.wall_clock(points["time"]), y=points["mensagens"], name=name, mode="lines", line=dict(color=colors[name], shape="hv")
        )
    ui.count_axis(figure, frame["mensagens"].max(), "mensagens")
    ui.time_axis(figure)
    figure.update_layout(hovermode="x unified")
    ui.chart(figure, 280)


def _levels(levels: data.Reading) -> None:
    if not levels.ok:
        ui.unavailable(levels)
        return
    frame = levels.value
    if frame.empty:
        ui.empty("Nenhum log nas últimas 24 horas.")
        return
    order = [level for level in LEVELS if level in set(frame["valor"])]
    values = frame.set_index("valor").reindex(order)["logs"]
    total = values.sum()
    ui.bar_list(
        order,
        values.tolist(),
        [f"{formatting.number(value)} · {formatting.percent(value / total)}" for value in values],
        colors=[LEVEL_COLORS[level] for level in order],
    )
    ui.note(f"{formatting.number(total)} logs de todos os serviços nas últimas 24 horas.")


def _errors(errors: data.Reading) -> None:
    if not errors.ok:
        ui.unavailable(errors)
        return
    if errors.value.empty:
        ui.empty("Nenhum log de erro nas últimas 24 horas.")
        return
    ui.events([(formatting.moment(row["time"], seconds=True), row["serviço"], row["mensagem"][:220]) for _, row in errors.value.iterrows()])
