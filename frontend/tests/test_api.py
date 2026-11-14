import httpx
import pytest

from nexo_shared.api import ApiError, ApiUnavailable, NexoApi


def client(handler, sleeps=None) -> NexoApi:
    record = sleeps.append if sleeps is not None else (lambda seconds: None)
    return NexoApi("http://api:8080", transport=httpx.MockTransport(handler), sleep=record)


def test_empty_filters_are_not_sent():
    seen = []

    def handler(request: httpx.Request):
        seen.append(request.url)
        return httpx.Response(200, json={"items": [], "page": 1, "pageSize": 20, "totalItems": 0})

    client(handler).search_funds(search="", fund_type=None, sort="-netWorth")
    assert seen[0].path == "/api/v1/funds"
    assert dict(seen[0].params) == {"sort": "-netWorth", "page": "1", "pageSize": "20"}


def test_not_found_becomes_none():
    assert client(lambda request: httpx.Response(404)).fund(1) is None


def test_errors_carry_the_problem_detail():
    api = client(lambda request: httpx.Response(400, json={"title": "Pedido inválido", "errors": {"pageSize": ["máximo 100"]}}))
    with pytest.raises(ApiError) as error:
        api.dashboard()
    assert error.value.status_code == 400
    assert "Pedido inválido" in str(error.value)


def test_waits_and_retries_when_rate_limited():
    answers = iter([httpx.Response(429, headers={"Retry-After": "2"}), httpx.Response(200, json={"funds": 1})])
    sleeps = []
    assert client(lambda request: next(answers), sleeps).dashboard() == {"funds": 1}
    assert sleeps == [2.0]


def test_connection_failure_is_reported_as_unavailable():
    def handler(request):
        raise httpx.ConnectError("recusada", request=request)

    with pytest.raises(ApiUnavailable):
        client(handler).dashboard()


def test_health_lives_outside_the_versioned_path():
    seen = []

    def handler(request: httpx.Request):
        seen.append(request.url.path)
        return httpx.Response(200)

    assert client(handler).health() is True
    assert seen == ["/health"]


def test_reprocess_sends_the_admin_key():
    def handler(request: httpx.Request):
        assert request.method == "POST"
        assert request.headers["X-Api-Key"] == "segredo"
        return httpx.Response(202)

    assert client(handler).reprocess(7, "segredo") == 202


def test_iterate_walks_pages_until_the_limit():
    def handler(request: httpx.Request):
        page = int(request.url.params["page"])
        items = [{"id": (page - 1) * 100 + index} for index in range(100)]
        return httpx.Response(200, json={"items": items, "page": page, "pageSize": 100, "totalItems": 250})

    api = client(handler)
    rows = list(api.iterate(api.search_funds, max_items=150))
    assert len(rows) == 150
    assert rows[-1]["id"] == 149
