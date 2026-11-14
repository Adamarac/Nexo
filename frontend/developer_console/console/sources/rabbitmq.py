"""Leitura da API de gerenciamento do RabbitMQ (a mesma que o painel web dele usa)."""

import json
from datetime import UTC, datetime
from urllib.parse import quote

import httpx
import pandas as pd

PROCESS_QUEUE = "nexo.process"
RETRY_QUEUE = "nexo.process.retry"
DEAD_LETTER_QUEUE = "nexo.process.dlq"

QUEUE_ROLES = {
    PROCESS_QUEUE: "arquivos para processar",
    RETRY_QUEUE: "espera da nova tentativa",
    DEAD_LETTER_QUEUE: "desistências (DLQ)",
}


class RabbitMq:
    def __init__(self, url: str, user: str, password: str, *, transport: httpx.BaseTransport | None = None):
        self._client = httpx.Client(base_url=f"{url.rstrip('/')}/api", auth=(user, password), timeout=10.0, transport=transport)

    def overview(self) -> dict:
        return self._get("/overview")

    def nodes(self) -> list[dict]:
        return self._get("/nodes")

    def queues(self, history_minutes: int = 60) -> list[dict]:
        # Pede ao RabbitMQ as amostras de tamanho e taxa da última hora, uma por minuto.
        seconds = history_minutes * 60
        return self._get(
            "/queues/%2F",
            params={"lengths_age": seconds, "lengths_incr": 60, "msg_rates_age": seconds, "msg_rates_incr": 60},
        )

    def consumers(self) -> list[dict]:
        return self._get("/consumers/%2F")

    def connections(self) -> list[dict]:
        return self._get("/connections")

    def peek(self, queue: str, count: int = 10) -> list[dict]:
        """Lê mensagens sem tirá-las da fila: o RabbitMQ entrega e devolve na hora (ack_requeue_true)."""
        body = {"count": count, "ackmode": "ack_requeue_true", "encoding": "auto", "truncate": 50_000}
        response = self._client.post(f"/queues/%2F/{quote(queue, safe='')}/get", json=body)
        response.raise_for_status()
        return response.json()

    def _get(self, path: str, params: dict | None = None):
        response = self._client.get(path, params=params)
        response.raise_for_status()
        return response.json()


def queue_table(queues: list[dict]) -> pd.DataFrame:
    rows = []
    for queue in queues:
        stats = queue.get("message_stats", {})
        arguments = queue.get("arguments", {})
        rows.append(
            {
                "fila": queue["name"],
                "papel": QUEUE_ROLES.get(queue["name"], "—"),
                "prontas": queue.get("messages_ready", 0),
                "em uso": queue.get("messages_unacknowledged", 0),
                "consumidores": queue.get("consumers", 0),
                "entrada /s": stats.get("publish_details", {}).get("rate", 0.0),
                "entrega /s": stats.get("deliver_get_details", {}).get("rate", 0.0),
                "confirmação /s": stats.get("ack_details", {}).get("rate", 0.0),
                "destino ao expirar": arguments.get("x-dead-letter-routing-key") or "—",
                "estado": queue.get("state", "—"),
            }
        )
    return pd.DataFrame(rows)


def queue_history(queues: list[dict]) -> pd.DataFrame:
    """Tamanho de cada fila ao longo do tempo, a partir das amostras do RabbitMQ."""
    rows = []
    for queue in queues:
        for sample in queue.get("messages_details", {}).get("samples", []):
            rows.append(
                {
                    "time": datetime.fromtimestamp(sample["timestamp"] / 1000, tz=UTC),
                    "fila": queue["name"],
                    "mensagens": sample["sample"],
                }
            )
    return pd.DataFrame(rows, columns=["time", "fila", "mensagens"])


def host_of(connection_name: str | None) -> str | None:
    """O Nexo batiza a conexão como 'nexo@<container>'; daqui sai o nome do container."""
    if not connection_name or "@" not in connection_name:
        return None
    return connection_name.split("@", 1)[1]


def consumers_by_host(consumers: list[dict], connections: list[dict]) -> dict[str, list[dict]]:
    """Junta cada consumidor com o container que abriu a conexão dele."""
    names = {connection["name"]: host_of(connection.get("client_properties", {}).get("connection_name")) for connection in connections}
    hosts: dict[str, list[dict]] = {}
    for consumer in consumers:
        connection_name = consumer.get("channel_details", {}).get("connection_name")
        host = names.get(connection_name) or connection_name or "desconhecido"
        hosts.setdefault(host, []).append(
            {
                "fila": consumer.get("queue", {}).get("name"),
                "prefetch": consumer.get("prefetch_count"),
                "ativo": consumer.get("active", True),
            }
        )
    return hosts


def connection_table(connections: list[dict]) -> pd.DataFrame:
    rows = []
    for connection in connections:
        rows.append(
            {
                "container": host_of(connection.get("client_properties", {}).get("connection_name")) or "—",
                "endereço": connection.get("peer_host", "—"),
                "estado": connection.get("state", "—"),
                "canais": connection.get("channels", 0),
                "recebido /s": connection.get("recv_oct_details", {}).get("rate", 0.0),
                "enviado /s": connection.get("send_oct_details", {}).get("rate", 0.0),
                "aberta desde": _from_millis(connection.get("connected_at")),
            }
        )
    return pd.DataFrame(rows)


def describe_message(message: dict) -> dict:
    """Resumo legível de uma mensagem lida da fila: qual arquivo, quantas vezes falhou e por quê."""
    properties = message.get("properties", {})
    headers = properties.get("headers") or {}
    try:
        body = json.loads(message.get("payload", "")) if message.get("payload_encoding") == "string" else {}
    except ValueError:
        body = {}

    return {
        "arquivo": body.get("datasetFileId"),
        "origem": body.get("sourceKey"),
        "correlação": body.get("correlationId") or properties.get("correlation_id"),
        "mensagem": body.get("messageId") or properties.get("message_id"),
        "publicada em": body.get("occurredAt"),
        # O Nexo conta as tentativas no próprio cabeçalho x-attempt.
        "tentativas": headers.get("x-attempt"),
        "último erro": headers.get("x-last-error"),
        "entregue antes": message.get("redelivered", False),
        "cabeçalhos": headers,
        "corpo": body or message.get("payload"),
    }


def _from_millis(value) -> datetime | None:
    return datetime.fromtimestamp(value / 1000, tz=UTC) if value else None
