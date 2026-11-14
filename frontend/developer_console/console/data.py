"""Ponto único de leitura das fontes. Cada leitura guarda o resultado ou o erro, e a hora em que aconteceu."""

from dataclasses import dataclass
from datetime import UTC, datetime
from typing import Any

import httpx
import streamlit as st

from console import settings
from console.sources.cache import Cache
from console.sources.database import Database
from console.sources.files import StoredFiles
from console.sources.loki import Loki
from console.sources.prometheus import Prometheus
from console.sources.rabbitmq import RabbitMq
from nexo_shared.api import ApiError, NexoApi

SOURCE_NAMES = {
    "api": "API",
    "rabbitmq": "RabbitMQ",
    "prometheus": "Prometheus",
    "loki": "Loki",
    "database": "PostgreSQL",
    "redis": "Redis",
    "files": "Disco",
}

# Por quantos segundos uma leitura é reaproveitada antes de ir à fonte de novo.
FRESHNESS_SECONDS = 10


@dataclass(frozen=True)
class Reading:
    source: str
    value: Any
    error: str | None
    read_at: datetime

    @property
    def ok(self) -> bool:
        return self.error is None

    @property
    def source_name(self) -> str:
        return SOURCE_NAMES.get(self.source, self.source)


@st.cache_resource
def clients() -> dict[str, Any]:
    config = settings.load()
    return {
        "api": NexoApi(config.api_url),
        "rabbitmq": RabbitMq(config.rabbitmq_url, config.rabbitmq_user, config.rabbitmq_password),
        "prometheus": Prometheus(config.prometheus_url),
        "loki": Loki(config.loki_url),
        "database": Database(config.database_url or ""),
        "redis": Cache(config.redis_url),
        "files": StoredFiles(config.files_root),
    }


@st.cache_data(ttl=FRESHNESS_SECONDS, show_spinner=False)
def fetch(source: str, method: str, *args, **kwargs) -> Reading:
    """Chama clients()[source].method(*args). Se a fonte falhar, a tela recebe o erro em vez de um zero."""
    client = clients()[source]
    try:
        value = getattr(client, method)(*args, **kwargs)
        return Reading(source, value, None, datetime.now(UTC))
    except Exception as error:  # noqa: BLE001 - qualquer falha da fonte vira mensagem na tela
        return Reading(source, None, describe_error(error), datetime.now(UTC))


def describe_error(error: Exception) -> str:
    if isinstance(error, httpx.ConnectError | httpx.ConnectTimeout):
        return f"sem conexão ({error.request.url.host})"
    if isinstance(error, httpx.HTTPStatusError):
        return f"respondeu {error.response.status_code} em {error.request.url.path}"
    if isinstance(error, httpx.TimeoutException):
        return "demorou demais para responder"
    if isinstance(error, ApiError):
        return str(error)
    first_line = str(error).strip().splitlines()[0] if str(error).strip() else type(error).__name__
    return first_line[:240]


def refresh() -> None:
    """Esquece as leituras guardadas para a próxima execução buscar tudo de novo."""
    fetch.clear()
