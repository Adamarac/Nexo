"""Páginas da Análise e os grupos do menu lateral."""

import streamlit as st

from analytics.pages import assets, compare, fund, funds, issuers, panorama

PAGES = {
    "panorama": st.Page(panorama.render, title="Panorama", icon=":material/space_dashboard:", url_path="panorama", default=True),
    "funds": st.Page(funds.render, title="Fundos", icon=":material/account_balance:", url_path="fundos"),
    "fund": st.Page(fund.render, title="Análise do fundo", icon=":material/insights:", url_path="fundo"),
    "compare": st.Page(compare.render, title="Comparar fundos", icon=":material/compare_arrows:", url_path="comparar"),
    "assets": st.Page(assets.render, title="Ativos", icon=":material/token:", url_path="ativos"),
    "issuers": st.Page(issuers.render, title="Emissores", icon=":material/apartment:", url_path="emissores"),
}

GROUPS = {
    "Visão geral": [PAGES["panorama"]],
    "Fundos": [PAGES["funds"], PAGES["fund"], PAGES["compare"]],
    "Mercado": [PAGES["assets"], PAGES["issuers"]],
}
