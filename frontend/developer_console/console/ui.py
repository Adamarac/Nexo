"""Peças visuais do Console: o kit comum com as cores daqui, mais cabeçalho, estados e horários."""

import math
from collections.abc import Callable
from html import escape

import pandas as pd
import streamlit as st

from console import data, theme
from nexo_shared import formatting, ui_kit, viz
from nexo_shared.ui_kit import Kpi, Progress, card, chart, empty, note, pill, rows

__all__ = ["Kpi", "Progress", "card", "chart", "empty", "note", "pill", "rows"]

LIVE_INTERVAL_SECONDS = 15


def apply_style() -> None:
    ui_kit.apply_style(theme.TOKENS)
    st.html(f"""
<style>
  .con-services {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: .75rem; margin-top: .25rem; }}
  .con-service {{ background: #FFFFFF; border-radius: 12px; padding: .9rem 1rem; min-width: 0; }}
  .con-service-top {{ display: flex; justify-content: space-between; align-items: center; gap: .5rem; }}
  .con-service-name {{ font-weight: 600; font-size: 14px; }}
  .con-service-detail {{ font-size: 13px; color: {theme.MUTED}; margin-top: .35rem; line-height: 1.45; overflow-wrap: anywhere; }}
  .con-events {{ display: flex; flex-direction: column; }}
  .con-event {{ display: grid; grid-template-columns: 10.5rem 7.5rem 1fr; gap: .75rem; padding: .7rem 0; border-bottom: 1px solid {theme.TOKENS.line}; font-size: 14px; line-height: 1.45; }}
  .con-event:last-child {{ border-bottom: none; }}
  .con-event span:first-child {{ color: {theme.MUTED}; font-variant-numeric: tabular-nums; }}
  .con-event span:last-child {{ overflow-wrap: anywhere; }}
  .con-sources {{ font-size: 12px; color: {theme.MUTED}; line-height: 1.7; overflow-wrap: anywhere; }}
  @media (max-width: 640px) {{ .con-event {{ grid-template-columns: 1fr; gap: .15rem; }} }}
</style>
""")


def page_header(title: str, subtitle: str) -> bool:
    """Título da página, ligar a leitura contínua e atualizar agora. Devolve se está ao vivo."""
    text, live_column, button_column = st.columns([6, 1.3, 1.1], vertical_alignment="center")
    with text:
        ui_kit.page_header(title, subtitle, ["Console", title])
    live = live_column.toggle("ao vivo", key=f"live-{title}", help=f"Relê as fontes a cada {LIVE_INTERVAL_SECONDS} s")
    if button_column.button("Atualizar", key=f"refresh-{title}", width="stretch", icon=":material/refresh:"):
        data.refresh()
        st.rerun()
    return live


def run(body: Callable[[], None], live: bool) -> None:
    """Desenha o corpo da página; ao vivo, só esse pedaço se redesenha sozinho de tempos em tempos."""
    if live:
        st.fragment(run_every=LIVE_INTERVAL_SECONDS)(body)()
    else:
        body()


def kpis(items: list[Kpi]) -> None:
    ui_kit.kpis(items, theme.TOKENS)


def progress(items: list[Progress]) -> None:
    ui_kit.progress(items, theme.TOKENS)


def bar_list(labels, values, texts, hover=None, colors=None) -> None:
    ui_kit.bar_list(labels, values, texts, theme.TOKENS, hover=hover, colors=colors)


def legend(labels, colors, texts) -> None:
    ui_kit.legend(labels, colors, texts)


def sources(*readings: data.Reading) -> str:
    """Texto do canto do cartão: de onde veio o dado e quando, ou FORA se a fonte não respondeu."""
    parts = []
    for reading in readings:
        moment = formatting.moment(reading.read_at, seconds=True)[-8:]
        parts.append(f"{reading.source_name} · {moment}" if reading.ok else f"{reading.source_name} · FORA")
    return "  |  ".join(parts)


def unavailable(reading: data.Reading) -> None:
    st.html(f'<div class="kit-empty">{pill("FORA", theme.FAIL)} &nbsp;{escape(reading.source_name)}: {escape(reading.error or "")}</div>')


def state(word: str) -> str:
    return pill(word, theme.STATE_COLORS.get(word, theme.MUTED))


def services(items: list[tuple[str, str, str]]) -> None:
    """Grade de serviços: nome, estado escrito com cor e um detalhe."""
    cells = "".join(
        f'<div class="con-service"><div class="con-service-top"><span class="con-service-name">{escape(name)}</span>{state(word)}</div>'
        f'<div class="con-service-detail">{escape(detail)}</div></div>'
        for name, word, detail in items
    )
    st.html(f'<div class="con-services">{cells}</div>')


def events(items: list[tuple[str, str, str]]) -> None:
    body = "".join(
        f"<div class='con-event'><span>{escape(a)}</span><span>{escape(b)}</span><span>{escape(c)}</span></div>" for a, b, c in items
    )
    st.html(f'<div class="con-events">{body}</div>')


def status_style(frame: pd.DataFrame, column: str):
    """Pinta só o texto da coluna de estado; o resto da tabela fica neutro."""

    def paint(value):
        color = theme.STATE_COLORS.get(value) or {"Falhou": theme.FAIL, "Processando": theme.WARNING}.get(value)
        return f"color: {color}; font-weight: 600" if color else ""

    return frame.style.map(paint, subset=[column])


def local_time(values) -> pd.Series:
    """Converte datas (UTC) para o horário de Brasília, mantendo o tipo data para ordenar."""
    return pd.to_datetime(pd.Series(values), utc=True, errors="coerce").dt.tz_convert(formatting.BRAZIL)


def wall_clock(values) -> pd.Series:
    """Horário de Brasília sem fuso anexado: o Plotly desenha o relógio local em vez do UTC."""
    return local_time(values).dt.tz_localize(None)


def time_axis(figure) -> None:
    figure.update_xaxes(tickformat="%H:%M\n%d/%m", hoverformat="%d/%m %H:%M")


def time_column(label: str | None = None, seconds: bool = False):
    return st.column_config.DatetimeColumn(label, format="DD/MM/YYYY HH:mm:ss" if seconds else "DD/MM/YYYY HH:mm")


def count_column(label: str | None = None):
    return st.column_config.NumberColumn(label, format="localized")


def sidebar(config) -> None:
    """Endereços de cada fonte no pé da barra lateral, para quem precisa conferir de onde vem o dado."""
    with st.sidebar:
        lines = [
            f"api {config.api_url}",
            f"rabbitmq {config.rabbitmq_url}",
            f"prometheus {config.prometheus_url}",
            f"loki {config.loki_url}",
            f"redis {config.redis_url}",
            f"postgres {'configurado' if config.database_url else 'não configurado'}",
        ]
        st.html('<div class="con-sources"><b>Fontes</b><br>' + "<br>".join(escape(line) for line in lines) + "</div>")


def short_key(source_key: str) -> str:
    """'cda_fi_202610' vira '10/26' para caber no eixo; o cadastro vira 'cadastro'."""
    suffix = source_key.rsplit("_", 1)[-1]
    return (
        f"{suffix[4:]}/{suffix[2:4]}" if len(suffix) == 6 and suffix.isdigit() else source_key.replace("registro_fundo_classe", "cadastro")
    )


def share_text(part: int, total: int) -> str:
    """Percentual com duas casas que nunca arredonda para 100% quando falta alguma coisa."""
    if not total:
        return formatting.MISSING
    if part < total and part / total > 0.9999:
        return "> 99,99%"
    return formatting.percent(part / total, 2)


def donut(labels, values, colors, texts, center: str) -> None:
    """Rosca com o número principal no meio e a legenda em lista embaixo, como no design de referência."""
    ui_kit.chart(viz.donut(list(values), list(colors), center, theme.TOKENS.card), 220)
    ui_kit.legend(labels, colors, texts)


def count_axis(figure, peak: float, title: str) -> None:
    """Eixo de contagem em números inteiros, com uns quatro degraus; fila vazia mostra só 0 e 1."""
    figure.update_yaxes(title_text=title, tickformat="d", range=[0, max(1, peak * 1.15)], dtick=max(1, math.ceil(peak / 4)))
