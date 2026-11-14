"""Consultas à API do Nexo, guardadas por alguns minutos: carteira é dado mensal e muda pouco."""

import streamlit as st

from nexo_shared.api import ApiError, NexoApi
from nexo_shared.settings import api_url

CACHE_SECONDS = 300
EXPORT_LIMIT = 10_000


class DataUnavailable(Exception):
    """Os dados não puderam ser consultados. A mensagem é para o analista, sem termos técnicos."""


@st.cache_resource
def api() -> NexoApi:
    return NexoApi(api_url())


def _call(method: str, *args, **kwargs):
    try:
        return getattr(api(), method)(*args, **kwargs)
    except ApiError as error:
        raise DataUnavailable(str(error)) from error


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def dashboard() -> dict:
    return _call("dashboard")


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def funds(search=None, fund_type=None, situation=None, sort=None, page=1, page_size=50) -> dict:
    return _call("search_funds", search=search, fund_type=fund_type, situation=situation, sort=sort, page=page, page_size=page_size)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def fund(fund_id: int) -> dict | None:
    return _call("fund", fund_id)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def portfolio(fund_id: int, reference_date=None, class_id=None) -> dict | None:
    return _call("fund_portfolio", fund_id, reference_date=reference_date, class_id=class_id)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def history(fund_id: int, class_id=None) -> list[dict]:
    return _call("fund_history", fund_id, class_id=class_id)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def positions(portfolio_id: int, block=None, search=None, sort=None, page=1, page_size=50) -> dict:
    return _call("positions", portfolio_id, block=block, search=search, sort=sort, page=page, page_size=page_size)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def assets(search=None, block=None, page=1, page_size=50) -> dict:
    return _call("search_assets", search=search, block=block, page=page, page_size=page_size)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def asset(asset_id: int) -> dict | None:
    return _call("asset", asset_id)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def holders(asset_id: int, reference_date=None, page=1, page_size=50) -> dict:
    return _call("asset_holders", asset_id, reference_date=reference_date, page=page, page_size=page_size)


def holders_date(asset_id: int) -> str | None:
    """Mês dos detentores. Tenta primeiro o mês mais recente da base, que a API responde rápido;
    só se o ativo não estiver nele pede à API para procurar o último mês em que ele aparece."""
    latest = dashboard()["latestReferenceDate"]
    if latest and holders(asset_id, reference_date=latest, page_size=1)["totalItems"]:
        return latest
    items = holders(asset_id, page_size=1)["items"]
    return items[0]["referenceDate"] if items else None


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def concentration(top=20, grouped=False) -> dict:
    return _call("concentration", top=top, grouped=grouped)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def asset_groups(search=None, block=None, page=1, page_size=50) -> dict:
    return _call("search_asset_groups", search=search, block=block, page=page, page_size=page_size)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def asset_maturities(asset_id: int) -> dict | None:
    return _call("asset_maturities", asset_id)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner="Buscando todas as linhas…")
def all_rows(method: str, limit: int = EXPORT_LIMIT, *args, **params) -> list[dict]:
    """Busca todas as linhas de uma lista (até o limite), página por página, para a exportação."""
    client = api()
    fetch_page = getattr(client, method)
    try:
        pages = client.iterate(lambda **kwargs: fetch_page(*args, **kwargs), max_items=limit, **params)
        return list(pages)
    except ApiError as error:
        raise DataUnavailable(str(error)) from error


# Limite para análises que precisam da carteira inteira; carteiras maiores usam as primeiras posições por valor.
ANALYSIS_LIMIT = 5_000


def all_positions(portfolio_id: int) -> list[dict]:
    """Todas as posições de uma carteira (até o limite), da maior para a menor."""
    return all_rows("positions", ANALYSIS_LIMIT, portfolio_id, sort="-marketValue")


def all_holders(asset_id: int, reference_date) -> list[dict]:
    return all_rows("asset_holders", ANALYSIS_LIMIT, asset_id, reference_date=reference_date)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def issuers(search=None, min_spellings=None, sort=None, page=1, page_size=50) -> dict:
    return _call("search_issuers", search=search, min_spellings=min_spellings, sort=sort, page=page, page_size=page_size)


@st.cache_data(ttl=CACHE_SECONDS, show_spinner=False)
def issuer(document: str) -> dict | None:
    return _call("issuer", document)


def all_issuer_funds(document: str, spelling=None) -> list[dict]:
    return all_rows("issuer_funds", ANALYSIS_LIMIT, document, spelling=spelling)
