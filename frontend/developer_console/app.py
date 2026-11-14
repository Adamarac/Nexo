"""Nexo Console: rode com `streamlit run app.py` dentro desta pasta."""

from pathlib import Path

import streamlit as st

from console import navigation, settings, ui

st.set_page_config(page_title="Nexo Console", page_icon=":material/monitor_heart:", layout="wide", initial_sidebar_state="auto")
st.logo(str(Path(__file__).parent / "assets" / "logo.svg"), size="large")
ui.apply_style()

page = st.navigation(navigation.GROUPS, position="sidebar")
ui.sidebar(settings.load())
page.run()
