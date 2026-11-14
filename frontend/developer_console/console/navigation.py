"""Páginas do Console e os grupos do menu lateral."""

import streamlit as st

from console.pages import ingestion, logs, overview, queues, resources, storage, workers

PAGES = {
    "overview": st.Page(overview.render, title="Visão geral", icon=":material/space_dashboard:", url_path="visao-geral", default=True),
    "ingestion": st.Page(ingestion.render, title="Ingestão", icon=":material/download:", url_path="ingestao"),
    "workers": st.Page(workers.render, title="Workers", icon=":material/engineering:", url_path="workers"),
    "queues": st.Page(queues.render, title="Filas", icon=":material/inbox:", url_path="filas"),
    "resources": st.Page(resources.render, title="Recursos", icon=":material/monitoring:", url_path="recursos"),
    "storage": st.Page(storage.render, title="Armazenamento", icon=":material/database:", url_path="armazenamento"),
    "logs": st.Page(logs.render, title="Logs e eventos", icon=":material/receipt_long:", url_path="logs"),
}

GROUPS = {
    "Operação": [PAGES["overview"], PAGES["ingestion"], PAGES["workers"], PAGES["queues"]],
    "Infraestrutura": [PAGES["resources"], PAGES["storage"], PAGES["logs"]],
}
