"""Consultas ao Prometheus, que recebe as métricas da API e dos workers pelo OpenTelemetry."""

from datetime import UTC, datetime, timedelta

import httpx
import pandas as pd


class Prometheus:
    def __init__(self, url: str, *, transport: httpx.BaseTransport | None = None):
        self._client = httpx.Client(base_url=url.rstrip("/"), timeout=10.0, transport=transport)

    def ready(self) -> bool:
        return self._client.get("/-/ready").status_code == 200

    def instant(self, promql: str) -> pd.DataFrame:
        return vector_frame(self._query("/api/v1/query", {"query": promql}))

    def range(self, promql: str, minutes: int, step_seconds: int | None = None) -> pd.DataFrame:
        end = datetime.now(UTC)
        start = end - timedelta(minutes=minutes)
        # Cerca de 120 pontos por gráfico, nunca menos de 15 s entre eles.
        step = step_seconds or max(15, minutes * 60 // 120)
        params = {"query": promql, "start": start.timestamp(), "end": end.timestamp(), "step": step}
        return matrix_frame(self._query("/api/v1/query_range", params))

    def _query(self, path: str, params: dict) -> list[dict]:
        response = self._client.get(path, params=params)
        response.raise_for_status()
        body = response.json()
        if body.get("status") != "success":
            raise RuntimeError(body.get("error", "Prometheus recusou a consulta"))
        return body["data"]["result"]


def vector_frame(result: list[dict]) -> pd.DataFrame:
    """Uma linha por série: os rótulos viram colunas e o valor atual em 'value'."""
    rows = []
    for series in result:
        timestamp, value = series["value"]
        rows.append({**series["metric"], "time": _moment(timestamp), "value": float(value)})
    return pd.DataFrame(rows)


def matrix_frame(result: list[dict]) -> pd.DataFrame:
    """Uma linha por ponto no tempo de cada série, pronta para gráfico de linha."""
    rows = []
    for series in result:
        for timestamp, value in series["values"]:
            rows.append({**series["metric"], "time": _moment(timestamp), "value": float(value)})
    return pd.DataFrame(rows)


def _moment(timestamp: float) -> datetime:
    return datetime.fromtimestamp(float(timestamp), tz=UTC)
