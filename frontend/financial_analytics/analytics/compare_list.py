"""Fundos escolhidos para comparar, guardados na sessão do navegador."""

import streamlit as st

LIMIT = 4
_KEY = "compare-funds"


def ids() -> list[int]:
    return list(st.session_state.get(_KEY, []))


def contains(fund_id: int) -> bool:
    return fund_id in ids()


def is_full() -> bool:
    return len(ids()) >= LIMIT


def add(fund_id: int) -> None:
    if not contains(fund_id) and not is_full():
        st.session_state[_KEY] = [*ids(), fund_id]


def remove(fund_id: int) -> None:
    st.session_state[_KEY] = [item for item in ids() if item != fund_id]


def load_from_link() -> None:
    """Um link com ?fundos=1,2,3 abre a comparação já montada, para o analista compartilhar."""
    raw = st.query_params.get("fundos", "")
    if raw and _KEY not in st.session_state:
        st.session_state[_KEY] = [int(part) for part in raw.split(",") if part.isdigit()][:LIMIT]


def write_link() -> None:
    if ids():
        st.query_params["fundos"] = ",".join(str(fund_id) for fund_id in ids())
    else:
        st.query_params.pop("fundos", None)
