"""Cliente da API do Nexo. Cada método corresponde a um endpoint de /api/v1, sem lógica extra."""

import time
from collections.abc import Callable, Iterator
from typing import Any

import httpx

MAX_PAGE_SIZE = 100


class ApiError(Exception):
    def __init__(self, message: str, status_code: int | None = None):
        super().__init__(message)
        self.status_code = status_code


class ApiUnavailable(ApiError):
    """A API não respondeu: está fora do ar ou a rede falhou."""


class NexoApi:
    def __init__(
        self,
        base_url: str,
        *,
        timeout: float = 20.0,
        max_retries: int = 4,
        transport: httpx.BaseTransport | None = None,
        sleep: Callable[[float], None] = time.sleep,
    ):
        self._root = base_url.rstrip("/")
        self._client = httpx.Client(base_url=f"{self._root}/api/v1", timeout=timeout, transport=transport)
        self._max_retries = max_retries
        self._sleep = sleep

    # ---- Painel e fundos ----

    def dashboard(self) -> dict:
        return self._get("/dashboard")

    def search_funds(self, *, search=None, fund_type=None, situation=None, sort=None, page=1, page_size=20) -> dict:
        return self._get("/funds", search=search, type=fund_type, situation=situation, sort=sort, page=page, pageSize=page_size)

    def fund(self, fund_id: int) -> dict | None:
        return self._get_or_none(f"/funds/{fund_id}")

    def fund_portfolio(self, fund_id: int, *, reference_date=None, class_id=None) -> dict | None:
        return self._get_or_none(f"/funds/{fund_id}/portfolio", referenceDate=reference_date, classId=class_id)

    def fund_history(self, fund_id: int, *, class_id=None) -> list[dict]:
        return self._get(f"/funds/{fund_id}/portfolio/history", classId=class_id)

    # ---- Carteiras e ativos ----

    def portfolio(self, portfolio_id: int) -> dict | None:
        return self._get_or_none(f"/portfolios/{portfolio_id}")

    def positions(self, portfolio_id: int, *, block=None, application_type=None, search=None, sort=None, page=1, page_size=20) -> dict:
        return self._get(
            f"/portfolios/{portfolio_id}/positions",
            block=block,
            applicationType=application_type,
            search=search,
            sort=sort,
            page=page,
            pageSize=page_size,
        )

    def search_assets(self, *, search=None, block=None, page=1, page_size=20) -> dict:
        return self._get("/assets", search=search, block=block, page=page, pageSize=page_size)

    def asset(self, asset_id: int) -> dict | None:
        return self._get_or_none(f"/assets/{asset_id}")

    def asset_holders(self, asset_id: int, *, reference_date=None, page=1, page_size=20) -> dict:
        return self._get(f"/assets/{asset_id}/holders", referenceDate=reference_date, page=page, pageSize=page_size)

    def concentration(self, *, reference_date=None, top=20) -> dict:
        return self._get("/analytics/concentration", referenceDate=reference_date, top=top)

    # ---- Ingestão ----

    def ingestion_status(self) -> dict:
        return self._get("/ingestion/status")

    def ingestion_files(self, *, status=None, page=1, page_size=20) -> dict:
        return self._get("/ingestion/files", status=status, page=page, pageSize=page_size)

    def ingestion_file(self, file_id: int) -> dict | None:
        return self._get_or_none(f"/ingestion/files/{file_id}")

    def rejections(self, file_id: int, *, reason=None, page=1, page_size=20) -> dict:
        return self._get(f"/ingestion/files/{file_id}/rejections", reason=reason, page=page, pageSize=page_size)

    def reprocess(self, file_id: int, admin_key: str) -> int:
        """Pede para processar o arquivo de novo. Devolve o código HTTP para a tela explicar o resultado."""
        response = self._send("POST", f"/ingestion/files/{file_id}/reprocess", headers={"X-Api-Key": admin_key})
        return response.status_code

    def health(self) -> bool:
        # O /health fica fora de /api/v1; endereço completo faz o httpx ignorar a base.
        response = self._send("GET", f"{self._root}/health")
        return response.status_code == 200

    # ---- Paginação ----

    def iterate(self, fetch_page: Callable[..., dict], *, max_items: int, **params) -> Iterator[dict]:
        """Percorre as páginas de uma lista até acabar ou chegar em max_items. Usado na exportação."""
        page, delivered = 1, 0

        while delivered < max_items:
            result = fetch_page(page=page, page_size=MAX_PAGE_SIZE, **params)
            for item in result["items"][: max_items - delivered]:
                delivered += 1
                yield item

            if page * MAX_PAGE_SIZE >= result["totalItems"] or not result["items"]:
                return
            page += 1

    # ---- Transporte ----

    def _get(self, path: str, **params) -> Any:
        response = self._send("GET", path, params=params)
        _raise_for_status(response)
        return response.json()

    def _get_or_none(self, path: str, **params) -> Any:
        response = self._send("GET", path, params=params)
        if response.status_code == 404:
            return None
        _raise_for_status(response)
        return response.json()

    def _send(self, method: str, path: str, *, params: dict | None = None, headers: dict | None = None) -> httpx.Response:
        query = {key: value for key, value in (params or {}).items() if value not in (None, "")}

        for attempt in range(self._max_retries + 1):
            try:
                response = self._client.request(method, path, params=query, headers=headers)
            except httpx.TransportError as error:
                raise ApiUnavailable(f"API do Nexo fora do ar: {error}") from error

            # A API limita pedidos por segundo; quando pede para esperar, a gente espera e tenta de novo.
            if response.status_code != 429 or attempt == self._max_retries:
                return response
            self._sleep(_retry_after(response))

        return response


def _retry_after(response: httpx.Response) -> float:
    try:
        return min(max(float(response.headers.get("Retry-After", "1")), 0.5), 10.0)
    except ValueError:
        return 1.0


def _raise_for_status(response: httpx.Response) -> None:
    if response.is_success:
        return

    detail = response.text[:300]
    try:
        body = response.json()
        detail = body.get("detail") or body.get("title") or str(body.get("errors", detail))
    except ValueError:
        pass
    raise ApiError(f"API respondeu {response.status_code}: {detail}", response.status_code)
