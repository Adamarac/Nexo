"""Nexo Análise: rode com `streamlit run app.py` dentro desta pasta."""

from pathlib import Path

import streamlit as st

from analytics import navigation, ui
from analytics.data import DataUnavailable

st.set_page_config(page_title="Nexo Análise", page_icon=":material/insights:", layout="wide", initial_sidebar_state="auto")
st.logo(str(Path(__file__).parent / "assets" / "logo.svg"), size="large")
ui.apply_style()

page = st.navigation(navigation.GROUPS, position="sidebar")
with st.sidebar:
    st.caption("Dados públicos da CVM, mensais e históricos. Não são cotações nem recomendação de investimento.")
try:
    page.run()
except DataUnavailable as error:
    # Uma falha da API vira um aviso simples na página, não uma tela de erro do Python.
    ui.unavailable(error)
