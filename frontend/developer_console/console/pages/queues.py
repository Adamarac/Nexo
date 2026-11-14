"""Filas: o que está esperando no RabbitMQ, o ritmo das mensagens e o que caiu na DLQ."""

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

from console import data, theme, ui
from console.data import fetch
from console.sources.rabbitmq import DEAD_LETTER_QUEUE, QUEUE_ROLES, describe_message, queue_history, queue_table
from nexo_shared import formatting

PEEK_LIMIT = 20

# Contadores que os serviços publicam a cada mensagem; somados entre todas as instâncias.
MESSAGE_COUNTERS = {
    "Publicadas": "nexo_messaging_published_total",
    "Consumidas": "nexo_messaging_consumed_total",
    "Nova tentativa": "nexo_messaging_retried_total",
    "Mandadas à DLQ": "nexo_messaging_dead_lettered_total",
}


def render() -> None:
    live = ui.page_header("Filas", "Mensagens no RabbitMQ, ritmo de entrada e saída e desistências")
    ui.run(_body, live)


def _body() -> None:
    queues = fetch("rabbitmq", "queues")
    if not queues.ok:
        with ui.card("down", "Filas"):
            ui.unavailable(queues)
        return
    table = queue_table(queues.value)
    dead = int(table.loc[table["fila"] == DEAD_LETTER_QUEUE, "prontas"].sum())
    ui.kpis(
        [
            ui.Kpi("Prontas", formatting.number(int(table["prontas"].sum())), "esperando um worker"),
            ui.Kpi("Em uso", formatting.number(int(table["em uso"].sum())), "entregues, sem confirmação"),
            ui.Kpi("Consumidores", formatting.number(int(table["consumidores"].sum())), "somando as filas"),
            ui.Kpi(
                "Na DLQ",
                formatting.number(dead),
                "esgotaram as tentativas" if dead else "nenhuma desistência",
            ),
        ]
    )

    columns = st.columns(len(table), gap="medium")
    for column, (_, row) in zip(columns, table.iterrows(), strict=True):
        with column, ui.card(f"queue-{row['fila'].replace('.', '-')}", row["fila"], QUEUE_ROLES.get(row["fila"], "")):
            ui.rows(
                {
                    "Prontas": formatting.number(row["prontas"]),
                    "Em uso": formatting.number(row["em uso"]),
                    "Consumidores": formatting.number(row["consumidores"]),
                    "Entrada": f"{formatting.number(row['entrada /s'], 2)} /s",
                    "Entrega": f"{formatting.number(row['entrega /s'], 2)} /s",
                    "Ao expirar vai para": row["destino ao expirar"],
                }
            )

    left, right = st.columns([3, 2], gap="medium")
    with left, ui.card("history", "Mensagens em cada fila · última hora", ui.sources(queues)):
        _history(queues)
    with right:
        counters = {label: fetch("prometheus", "instant", f"sum(increase({metric}[24h]))") for label, metric in MESSAGE_COUNTERS.items()}
        with ui.card("counters", "Mensagens nas últimas 24 h", ui.sources(next(iter(counters.values())))):
            _counters(counters)

    with ui.card("dlq", "Desistências · DLQ", ui.sources(queues)):
        _dead_letters(queues, dead)
    ui.note(
        "A fila de espera não tem consumidor: a mensagem fica lá até o prazo vencer e volta para nexo.process. Depois da última tentativa, vai para a DLQ."
    )


def _history(queues: data.Reading) -> None:
    frame = queue_history(queues.value)
    if frame.empty:
        ui.empty("O RabbitMQ ainda não tem amostras de histórico.")
        return
    colors = theme.color_map(frame["fila"])
    figure = go.Figure()
    for name, points in frame.groupby("fila"):
        figure.add_scatter(
            x=ui.wall_clock(points["time"]),
            y=points["mensagens"],
            name=name,
            mode="lines",
            line=dict(color=colors[name], shape="hv"),
        )
    ui.count_axis(figure, frame["mensagens"].max(), "mensagens")
    ui.time_axis(figure)
    figure.update_layout(hovermode="x unified")
    ui.chart(figure, 300)


def _counters(counters: dict) -> None:
    labels_, values = [], []
    for label, reading in counters.items():
        frame = reading.value if reading.ok else pd.DataFrame()
        labels_.append(label)
        values.append(float(frame["value"].iloc[0]) if not frame.empty else 0.0)
    if not any(values):
        ui.empty("Nenhum evento registrado nas últimas 24 horas.")
        return
    ui.bar_list(
        labels_,
        values,
        [formatting.number(round(value)) for value in values],
        colors=[theme.SERIES[0], theme.SERIES[2], theme.SERIES[3], theme.FAIL],
    )
    ui.note("Os contadores recomeçam quando um serviço reinicia; o valor é uma estimativa do Prometheus.")


def _dead_letters(queues: data.Reading, waiting: int) -> None:
    if not any(queue["name"] == DEAD_LETTER_QUEUE for queue in queues.value):
        ui.empty(f"A fila {DEAD_LETTER_QUEUE} não existe no RabbitMQ.")
        return
    if not waiting:
        ui.empty("A DLQ está vazia: nenhuma mensagem esgotou as tentativas.")
        return
    if st.button(f"Espiar até {PEEK_LIMIT} mensagens", help="Lê e devolve à fila na hora; nada é apagado.", icon=":material/visibility:"):
        try:
            peeked = data.clients()["rabbitmq"].peek(DEAD_LETTER_QUEUE, PEEK_LIMIT)
            st.session_state["dlq-peek"] = [describe_message(message) for message in peeked]
        except Exception as error:  # noqa: BLE001 - mostra o erro da fonte em vez de quebrar a página
            st.session_state["dlq-peek"] = data.describe_error(error)

    peek = st.session_state.get("dlq-peek")
    if isinstance(peek, str):
        ui.empty(f"RabbitMQ não respondeu: {peek}")
    elif peek:
        table = pd.DataFrame(peek)
        st.dataframe(
            table.drop(columns=["cabeçalhos", "corpo"]),
            hide_index=True,
            width="stretch",
            column_config={"último erro": st.column_config.TextColumn(width="large")},
        )
        ui.note("Para tentar de novo, abra o arquivo na página de ingestão e use 'Reprocessar'.")
        from console.navigation import PAGES

        for message in peek:
            if message["arquivo"]:
                st.page_link(
                    PAGES["ingestion"],
                    label=f"Abrir arquivo {message['arquivo']} · {message['origem']}",
                    icon=":material/open_in_new:",
                    query_params={"arquivo": str(message["arquivo"])},
                )
