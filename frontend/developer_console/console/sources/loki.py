"""Busca de logs no Loki. Os serviços mandam os logs pelo OpenTelemetry, com as propriedades do Serilog."""

from dataclasses import dataclass
from datetime import UTC, datetime, timedelta

import httpx
import pandas as pd

SERVICES = ("nexo-api", "nexo-worker")
LEVELS = ("Debug", "Information", "Warning", "Error", "Fatal")

# Atributos que todo log tem e não ajudam a investigar nada.
_NOISE = {"telemetry_sdk_language", "telemetry_sdk_name", "telemetry_sdk_version", "observed_timestamp", "severity_number"}


@dataclass(frozen=True)
class LogFilter:
    services: tuple[str, ...] = SERVICES
    levels: tuple[str, ...] = ()
    text: str = ""
    source_key: str = ""
    correlation_id: str = ""
    instance: str = ""


def build_query(log_filter: LogFilter) -> str:
    """Monta a consulta LogQL a partir dos filtros da tela."""
    services = "|".join(log_filter.services or SERVICES)
    selector = f'service_name=~"{services}"'
    if log_filter.instance:
        selector += f', service_instance_id="{_quote(log_filter.instance)}"'

    query = "{" + selector + "}"
    if log_filter.levels:
        query += f' | severity_text=~"{"|".join(log_filter.levels)}"'
    if log_filter.source_key.strip():
        query += f' | SourceKey="{_quote(log_filter.source_key.strip())}"'
    if log_filter.correlation_id.strip():
        query += f' | CorrelationId="{_quote(log_filter.correlation_id.strip())}"'
    if log_filter.text.strip():
        # (?i) faz a busca ignorar maiúsculas; os símbolos de expressão regular viram texto comum.
        query += f' |~ "(?i){_quote(_literal(log_filter.text.strip()))}"'
    return query


def count_query(log_filter: LogFilter, step_seconds: int) -> str:
    return f"sum by (severity_text) (count_over_time({build_query(log_filter)} [{step_seconds}s]))"


class Loki:
    def __init__(self, url: str, *, transport: httpx.BaseTransport | None = None):
        self._client = httpx.Client(base_url=url.rstrip("/"), timeout=15.0, transport=transport)

    def ready(self) -> bool:
        return self._client.get("/ready").status_code == 200

    def logs(self, log_filter: LogFilter, minutes: int, limit: int = 300) -> pd.DataFrame:
        result = self._range(build_query(log_filter), minutes, {"limit": limit, "direction": "backward"})
        return log_frame(result)

    def counts(self, log_filter: LogFilter, minutes: int) -> pd.DataFrame:
        # Cerca de 60 barras no período, nunca menos de um minuto cada.
        step = max(60, minutes)
        result = self._range(count_query(log_filter, step), minutes, {"step": step})
        rows = [
            {"time": _from_seconds(timestamp), "nível": series["metric"].get("severity_text", "—"), "logs": int(float(value))}
            for series in result
            for timestamp, value in series["values"]
        ]
        return pd.DataFrame(rows, columns=["time", "nível", "logs"])

    def totals(self, log_filter: LogFilter, minutes: int, by: str) -> pd.DataFrame:
        """Quantos logs há no período, agrupados por um atributo (nível, modelo da mensagem...)."""
        query = f"sum by ({by}) (count_over_time({build_query(log_filter)} [{minutes}m]))"
        response = self._client.get("/loki/api/v1/query", params={"query": query, "time": _nanos(datetime.now(UTC))})
        if response.status_code >= 400:
            raise RuntimeError(f"Loki recusou a consulta: {response.text[:300]}")
        rows = [
            {"valor": series["metric"].get(by, "—"), "logs": int(float(series["value"][1]))} for series in response.json()["data"]["result"]
        ]
        return pd.DataFrame(rows, columns=["valor", "logs"]).sort_values("logs", ascending=False, ignore_index=True)

    def _range(self, query: str, minutes: int, extra: dict) -> list[dict]:
        end = datetime.now(UTC)
        start = end - timedelta(minutes=minutes)
        params = {"query": query, "start": _nanos(start), "end": _nanos(end), **extra}
        response = self._client.get("/loki/api/v1/query_range", params=params)
        if response.status_code >= 400:
            raise RuntimeError(f"Loki recusou a consulta: {response.text[:300]}")
        return response.json()["data"]["result"]


def log_frame(streams: list[dict]) -> pd.DataFrame:
    """Uma linha por log, do mais novo para o mais antigo, com as propriedades mais úteis em colunas."""
    columns = ["time", "serviço", "instância", "nível", "mensagem", "SourceKey", "CorrelationId", "origem", "atributos"]
    rows = []
    for stream in streams:
        labels = stream["stream"]
        for timestamp, line in stream["values"]:
            rows.append(
                {
                    "time": _from_nanos(timestamp),
                    "serviço": labels.get("service_name", "—"),
                    "instância": labels.get("service_instance_id", "—"),
                    "nível": labels.get("severity_text") or labels.get("detected_level", "—"),
                    "mensagem": line,
                    "SourceKey": labels.get("SourceKey", ""),
                    "CorrelationId": labels.get("CorrelationId", ""),
                    "origem": labels.get("scope_name", ""),
                    "atributos": {key: value for key, value in labels.items() if key not in _NOISE},
                }
            )

    frame = pd.DataFrame(rows, columns=columns)
    return frame.sort_values("time", ascending=False, ignore_index=True)


def _quote(text: str) -> str:
    return text.replace("\\", "\\\\").replace('"', '\\"')


def _nanos(moment: datetime) -> int:
    return int(moment.timestamp() * 1_000_000_000)


def _from_nanos(value: str) -> datetime:
    return datetime.fromtimestamp(int(value) / 1_000_000_000, tz=UTC)


def _from_seconds(value) -> datetime:
    return datetime.fromtimestamp(float(value), tz=UTC)


_REGEX_SYMBOLS = set(r"\.+*?()|[]{}^$")


def _literal(text: str) -> str:
    """Escapa só os símbolos de expressão regular. O re.escape também escapa espaços, e o Loki recusa isso."""
    return "".join("\\" + char if char in _REGEX_SYMBOLS else char for char in text)
