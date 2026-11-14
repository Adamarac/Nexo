"""Ingestão: cada arquivo baixado da CVM, o que aconteceu com ele e as linhas que foram recusadas."""

import math
from html import escape

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

from console import data, settings, theme, ui
from console.data import fetch
from nexo_shared import formatting, labels
from nexo_shared.api import ApiError

PAGE_SIZE = 50
ALL = "Todos"

REPROCESS_RESULTS = {
    202: ("OK", "Pedido aceito. O arquivo voltou para 'Baixado' e um worker vai pegá-lo pela fila."),
    404: ("FALHA", "A API não encontrou esse arquivo."),
    409: ("ATENÇÃO", "Recusado: um worker está com esse arquivo agora ou ele já foi substituído por uma versão mais nova."),
    401: ("FALHA", "A API recusou a chave de administrador."),
    403: ("FALHA", "A API recusou a chave de administrador."),
}


def render() -> None:
    live = ui.page_header("Ingestão", "Arquivos baixados da CVM, linhas lidas e recusadas, e reprocessamento")
    ui.run(_body, live)


def _body() -> None:
    every_file = fetch("api", "ingestion_files", page_size=100)
    _indicators(every_file)

    left, right = st.columns([2, 1], gap="medium")
    with left, ui.card("rows", "Linhas lidas por arquivo", ui.sources(every_file)):
        _rows_per_file(every_file)
    with right:
        reasons = fetch("database", "rejection_reasons")
        with ui.card("reasons", "Motivos de rejeição", ui.sources(reasons)):
            _reasons(reasons)

    with ui.card("files", "Arquivos", "clique numa linha para ver o detalhe"):
        selected = _files_table()
    if selected is not None:
        _file_detail(selected)


def _indicators(files: data.Reading) -> None:
    if not files.ok:
        ui.kpis([ui.Kpi("Arquivos", "—", files.error or "")])
        return
    items = files.value["items"]
    read = sum(item["rowsRead"] for item in items)
    accepted = sum(item["rowsAccepted"] for item in items)
    rejected = sum(item["rowsRejected"] for item in items)
    ui.kpis(
        [
            ui.Kpi("Arquivos", formatting.number(files.value["totalItems"]), "baixados da CVM"),
            ui.Kpi("Linhas lidas", formatting.compact(read), "somando todos os arquivos"),
            ui.Kpi(
                "Aceitas",
                ui.share_text(accepted, read),
                f"{formatting.number(accepted)} linhas",
            ),
            ui.Kpi("Rejeitadas", formatting.number(rejected), "com o motivo guardado"),
            ui.Kpi(
                "Carteiras alteradas",
                formatting.compact(sum(item["portfoliosChanged"] for item in items)),
                "revisões criadas",
            ),
        ]
    )


def _rows_per_file(files: data.Reading) -> None:
    if not files.ok:
        ui.unavailable(files)
        return
    frame = pd.DataFrame(files.value["items"])
    if frame.empty:
        ui.empty("Nenhum arquivo ainda.")
        return
    frame = frame.sort_values("sourceKey")
    figure = go.Figure(
        go.Bar(
            x=frame["sourceKey"].map(ui.short_key),
            y=frame["rowsRead"] / 1e3,
            marker_color=theme.SERIES[0],
            customdata=list(zip(frame["sourceKey"], frame["rowsRead"].map(formatting.number), frame["rowsRejected"], strict=True)),
            hovertemplate="%{customdata[0]}<br>%{customdata[1]} linhas · %{customdata[2]} rejeitadas<extra></extra>",
        )
    )
    figure.update_yaxes(title_text="mil linhas")
    figure.update_xaxes(type="category", nticks=12, tickangle=0)
    figure.update_layout(bargap=0.2)
    ui.chart(figure, 280)


def _reasons(reasons: data.Reading) -> None:
    if not reasons.ok:
        ui.unavailable(reasons)
        return
    if reasons.value.empty:
        ui.empty("Nenhuma linha rejeitada.")
        return
    frame = reasons.value
    total = frame["rows"].sum()
    ui.bar_list(
        [labels.rejection_reason(reason) for reason in frame["reason"]],
        frame["rows"].tolist(),
        [f"{value} · {formatting.percent(value / total)}" for value in frame["rows"]],
        colors=theme.ACCENT,
    )


def _files_table() -> int | None:
    status = fetch("api", "ingestion_status")
    counts = status.value["filesByStatus"] if status.ok else {}
    options = [ALL, *labels.FILE_STATUSES]

    def caption(option: str) -> str:
        if option == ALL:
            return f"todos {sum(counts.values())}" if counts else "todos"
        return f"{labels.file_status(option).lower()} {counts.get(option, 0)}"

    choice = st.segmented_control(
        "status",
        options,
        default=ALL,
        format_func=caption,
        key="ingestion-status",
        label_visibility="collapsed",
        on_change=lambda: st.session_state.update({"ingestion-page": 1}),
    )
    status_filter = None if choice in (None, ALL) else choice
    files = fetch("api", "ingestion_files", status=status_filter, page=st.session_state.get("ingestion-page", 1), page_size=PAGE_SIZE)
    if not files.ok:
        ui.unavailable(files)
        return None
    items = files.value["items"]
    if not items:
        ui.empty("Nenhum arquivo nessa situação.")
        return _selected_from_link()

    table = pd.DataFrame(
        [
            {
                "id": item["id"],
                "arquivo": item["sourceKey"],
                "tipo": labels.file_kind(item["kind"]),
                "status": labels.file_status(item["status"]),
                "tentativas": item["attempts"],
                "tamanho": formatting.size(item["size"]),
                "lidas": item["rowsRead"],
                "rejeitadas": item["rowsRejected"],
                "carteiras alteradas": item["portfoliosChanged"],
                "processado": item["processedAt"],
                "erro": (item["lastError"] or "").splitlines()[0][:120] if item["lastError"] else "",
            }
            for item in items
        ]
    )
    table["processado"] = ui.local_time(table["processado"])
    event = st.dataframe(
        ui.status_style(table, "status"),
        hide_index=True,
        width="stretch",
        height=min(40 + 35 * len(table), 460),
        on_select="rerun",
        selection_mode="single-row",
        key="ingestion-files",
        column_config={
            "id": st.column_config.NumberColumn(width="small"),
            "lidas": ui.count_column(),
            "rejeitadas": ui.count_column(),
            "carteiras alteradas": ui.count_column(),
            "processado": ui.time_column(),
        },
    )
    pages = max(1, math.ceil(files.value["totalItems"] / PAGE_SIZE))
    if pages > 1:
        st.number_input(f"página (de {pages})", min_value=1, max_value=pages, step=1, key="ingestion-page")
    if event.selection.rows:
        file_id = int(table.iloc[event.selection.rows[0]]["id"])
        st.query_params["arquivo"] = str(file_id)
        return file_id
    return _selected_from_link()


def _selected_from_link() -> int | None:
    value = st.query_params.get("arquivo")
    return int(value) if value and value.isdigit() else None


def _file_detail(file_id: int) -> None:
    reading = fetch("api", "ingestion_file", file_id)
    file = reading.value if reading.ok else None
    title = f"Arquivo {file_id}" + (f" · {file['sourceKey']}" if file else "")
    with ui.card("detail", title, ui.sources(reading)):
        if not reading.ok:
            ui.unavailable(reading)
            return
        if file is None:
            ui.empty("A API não encontrou esse arquivo.")
            return
        _show_reprocess_result()
        info, actions = st.columns([3, 1], gap="medium")
        with info:
            ui.rows(
                {
                    "Tipo": labels.file_kind(file["kind"]),
                    "Situação": labels.file_status(file["status"]),
                    "Tamanho": f"{formatting.size(file['size'])} ({formatting.number(file['size'])} bytes)",
                    "Hash do conteúdo": file["contentHash"],
                    "Tentativas": str(file["attempts"]),
                    "Linhas": f"{formatting.number(file['rowsRead'])} lidas · {formatting.number(file['rowsAccepted'])} aceitas · {formatting.number(file['rowsRejected'])} rejeitadas",
                    "Carteiras alteradas": formatting.number(file["portfoliosChanged"]),
                    "Descoberto": formatting.moment(file["discoveredAt"], seconds=True),
                    "Processado": formatting.moment(file["processedAt"], seconds=True),
                }
            )
        with actions:
            accepted = file["rowsAccepted"] / file["rowsRead"] if file["rowsRead"] else None
            ui.progress([ui.Progress("Linhas aceitas", ui.share_text(file["rowsAccepted"], file["rowsRead"]), accepted, theme.OK)])
            if st.button("Reprocessar", type="primary", width="stretch", key=f"reprocess-{file['id']}", icon=":material/replay:"):
                _confirm_reprocess(file)
            st.page_link(
                _logs_page(), label="Logs deste arquivo", icon=":material/receipt_long:", query_params={"source": file["sourceKey"]}
            )
        if file["lastError"]:
            st.caption("último erro")
            st.code(file["lastError"], language=None, wrap_lines=True)

    if file["rowsRejected"]:
        _rejections(file["id"], file["rowsRejected"])


def _rejections(file_id: int, total_rejected: int) -> None:
    with ui.card("rejections", f"Linhas rejeitadas · {formatting.number(total_rejected)}"):
        reasons = [ALL, *labels.REJECTION_REASONS]
        reason = st.selectbox(
            "motivo",
            reasons,
            format_func=lambda option: "todos os motivos" if option == ALL else labels.rejection_reason(option),
            key=f"reason-{file_id}",
        )
        page_key = f"rejections-page-{file_id}"
        reading = fetch(
            "api",
            "rejections",
            file_id,
            reason=None if reason == ALL else reason,
            page=st.session_state.get(page_key, 1),
            page_size=PAGE_SIZE,
        )
        if not reading.ok:
            ui.unavailable(reading)
            return
        items = reading.value["items"]
        if not items:
            ui.empty("Nenhuma linha rejeitada com esse motivo.")
            return
        table = pd.DataFrame(
            [
                {
                    "arquivo do zip": item["fileName"],
                    "linha": item["lineNumber"],
                    "motivo": labels.rejection_reason(item["reason"]),
                    "detalhe": item["detail"],
                    "conteúdo original": item["rawLine"],
                }
                for item in items
            ]
        )
        st.dataframe(
            table, hide_index=True, width="stretch", column_config={"conteúdo original": st.column_config.TextColumn(width="large")}
        )
        pages = max(1, math.ceil(reading.value["totalItems"] / PAGE_SIZE))
        if pages > 1:
            st.number_input(f"página (de {pages})", min_value=1, max_value=pages, step=1, key=page_key)


@st.dialog("Reprocessar arquivo")
def _confirm_reprocess(file: dict) -> None:
    st.markdown(
        f"`{file['sourceKey']}` (id {file['id']}) volta para **Baixado** e um novo aviso entra na fila. "
        "Um worker processa o arquivo de novo; carteiras iguais às atuais não geram revisão nova."
    )
    configured_key = settings.load().admin_api_key
    key = configured_key or st.text_input("chave de administrador", type="password", help="Valor de NEXO_ADMIN_API_KEY")
    if configured_key:
        st.caption("Usando a chave de administrador configurada no console.")
    if st.button("Confirmar", type="primary", disabled=not key, icon=":material/check:"):
        try:
            code = data.clients()["api"].reprocess(file["id"], key)
            word, message = REPROCESS_RESULTS.get(code, ("FALHA", f"A API respondeu {code}."))
        except ApiError as error:
            word, message = "FALHA", str(error)
        st.session_state["reprocess-result"] = (word, message)
        data.refresh()
        st.rerun()


def _show_reprocess_result() -> None:
    result = st.session_state.pop("reprocess-result", None)
    if result:
        word, message = result
        st.html(f'<div class="kit-note" style="font-size:.88rem">{ui.state(word)} &nbsp;Reprocessamento: {escape(message)}</div>')


def _logs_page():
    # Importado aqui para evitar ciclo: a navegação conhece todas as páginas.
    from console.navigation import PAGES

    return PAGES["logs"]
