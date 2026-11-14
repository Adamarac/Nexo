"""Peças visuais comuns às duas telas, no estilo do design de referência (ByeWind/SnowUI).

Página branca, cartões cinza-claro sem sombra, texto quase preto, nada menor que 12 px e nenhum texto cortado.
"""

from collections.abc import Iterator
from contextlib import contextmanager
from dataclasses import dataclass
from html import escape

import plotly.graph_objects as go
import streamlit as st

from nexo_shared import formatting


@dataclass(frozen=True)
class Tokens:
    """Cores e fontes de uma tela."""

    page: str
    card: str
    tint_a: str
    tint_b: str
    line: str
    text: str
    muted: str
    ink: str
    series: list[str]
    other: str
    font: str = "Inter, system-ui, sans-serif"


@dataclass(frozen=True)
class Kpi:
    label: str
    value: str
    note: str = ""


@dataclass(frozen=True)
class Progress:
    label: str
    value: str
    fraction: float | None
    color: str = ""


def apply_style(tokens: Tokens) -> None:
    """CSS da tela inteira. Os cartões são containers com chave começando por 'card'."""
    t = tokens
    st.html(f"""
<style>
  .stApp {{ background: {t.page}; }}
  .stApp, .stApp p, .stApp li, .stApp label, .stApp input, .stApp button {{ font-family: {t.font}; }}
  [data-testid="stMainBlockContainer"] {{ padding: 1.75rem 1.75rem 3rem; max-width: 1440px; }}
  [data-testid="stHeader"] {{ background: transparent; }}
  [data-testid="stSidebar"] {{ border-right: 1px solid {t.line}; }}
  [data-testid="stSidebarNav"] a {{ border-radius: 12px; padding-top: .3rem; padding-bottom: .3rem; }}
  [data-testid="stSidebarNav"] a span {{ font-size: 14px; color: {t.text}; }}
  [data-testid="stSidebarNavSeparator"] {{ display: none; }}
  .msr {{ font-family: 'Material Symbols Rounded'; font-weight: normal; font-style: normal; line-height: 1;
          display: inline-block; white-space: nowrap; direction: ltr; -webkit-font-smoothing: antialiased; }}

  /* Cartão: cinza-claro, cantos de 16 px, sem borda e sem sombra. */
  [class*="st-key-card"] {{ background: {t.card}; border-radius: 16px; padding: 1.5rem; }}
  .kit-card-head {{ display: flex; justify-content: space-between; align-items: baseline; gap: .75rem; flex-wrap: wrap; margin-bottom: .5rem; }}
  .kit-card-title {{ font-weight: 600; font-size: 14px; color: {t.text}; }}
  .kit-card-note {{ font-size: 12px; color: {t.muted}; }}

  .kit-crumbs {{ font-size: 14px; color: {t.muted}; display: flex; gap: .5rem; flex-wrap: wrap; }}
  .kit-crumbs span:last-child {{ color: {t.text}; }}
  .kit-title {{ font-weight: 600; font-size: 22px; color: {t.text}; line-height: 1.3; margin-top: .75rem; overflow-wrap: anywhere; }}
  .kit-subtitle {{ font-size: 14px; color: {t.muted}; margin-top: .25rem; line-height: 1.5; }}

  .kit-kpis {{ display: grid; grid-template-columns: repeat(var(--kit-columns, 4), minmax(0, 1fr)); gap: 1.75rem; margin: 1.25rem 0 1.75rem; }}
  @media (max-width: 1100px) {{ .kit-kpis {{ grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }} }}
  @media (max-width: 420px) {{ .kit-kpis {{ grid-template-columns: 1fr; }} }}
  .kit-kpi {{ border-radius: 16px; padding: 1.5rem; min-width: 0; }}
  .kit-kpi-top {{ display: flex; justify-content: space-between; align-items: center; gap: .5rem; font-size: 14px; color: {t.text}; }}
  .kit-kpi-top .msr {{ font-size: 18px; color: {t.text}; opacity: .55; }}
  .kit-kpi-value {{ font-size: 24px; font-weight: 600; color: {t.text}; line-height: 1.3; margin-top: .5rem; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }}
  .kit-kpi-note {{ font-size: 12px; color: {t.muted}; margin-top: .25rem; line-height: 1.4; }}

  .kit-bars {{ display: flex; flex-direction: column; gap: 1rem; margin-top: .25rem; }}
  .kit-bar-top {{ display: flex; justify-content: space-between; align-items: baseline; gap: 1rem; font-size: 13px; color: {t.text}; line-height: 1.4; }}
  .kit-bar-top span:first-child {{ overflow-wrap: anywhere; }}
  .kit-bar-top span:last-child {{ white-space: nowrap; font-variant-numeric: tabular-nums; color: {t.text}; }}
  .kit-track {{ height: 3px; border-radius: 2px; background: {t.line}; margin-top: .4rem; }}
  .kit-fill {{ height: 100%; border-radius: 2px; }}

  .kit-tag {{ display: inline-flex; align-items: center; gap: .4rem; font-size: 13px; color: {t.text}; white-space: nowrap; }}
  .kit-dot {{ width: 6px; height: 6px; border-radius: 50%; display: inline-block; flex: none; }}

  .kit-list {{ display: flex; flex-direction: column; }}
  .kit-row {{ display: flex; justify-content: space-between; gap: 1rem; padding: .6rem 0; border-bottom: 1px solid {t.line}; font-size: 14px; line-height: 1.4; }}
  .kit-row:last-child {{ border-bottom: none; }}
  .kit-row span:first-child {{ color: {t.muted}; }}
  .kit-row span:last-child {{ color: {t.text}; text-align: right; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }}

  .kit-legend {{ display: flex; flex-direction: column; gap: .6rem; margin-top: .5rem; }}
  .kit-legend-row {{ display: flex; justify-content: space-between; gap: 1rem; font-size: 13px; color: {t.text}; }}
  .kit-legend-row span:first-child {{ display: inline-flex; align-items: center; gap: .5rem; overflow-wrap: anywhere; }}
  .kit-legend-row span:last-child {{ white-space: nowrap; font-variant-numeric: tabular-nums; }}

  .kit-note {{ font-size: 12px; color: {t.muted}; margin: .5rem 0 0; line-height: 1.5; }}
  .kit-empty {{ font-size: 14px; color: {t.muted}; padding: 1.5rem 0; text-align: center; }}
  [data-testid="stDataFrame"] {{ border-radius: 8px; }}
  @media (max-width: 640px) {{
    [data-testid="stMainBlockContainer"] {{ padding: 3.5rem 1rem 2rem; }}
    [class*="st-key-card"] {{ padding: 1.1rem; }}
    .kit-title {{ font-size: 19px; }}
  }}
</style>
""")


def page_header(title: str, subtitle: str = "", crumbs: list[str] | None = None) -> None:
    path = "".join(f"<span>{escape(crumb)}</span><span>/</span>" for crumb in (crumbs or [])[:-1])
    last = f"<span>{escape(crumbs[-1])}</span>" if crumbs else ""
    subtitle_html = f'<div class="kit-subtitle">{subtitle}</div>' if subtitle else ""
    st.html(f'<div><div class="kit-crumbs">{path}{last}</div><div class="kit-title">{escape(title)}</div>{subtitle_html}</div>')


@contextmanager
def card(key: str, title: str = "", note: str = "") -> Iterator[None]:
    """Cartão com título opcional. Tudo que for desenhado dentro do 'with' fica no cartão."""
    with st.container(key=f"card-{key}"):
        if title or note:
            st.html(
                f'<div class="kit-card-head"><span class="kit-card-title">{escape(title)}</span>'
                f'<span class="kit-card-note">{escape(note)}</span></div>'
            )
        yield


def kpis(items: list[Kpi], tokens: Tokens) -> None:
    """Indicadores como no design: o primeiro com fundo azul-claro, o último com lavanda, os do meio cinza."""
    tints = [
        tokens.tint_a if index == 0 else tokens.tint_b if index == len(items) - 1 and len(items) >= 4 else tokens.card
        for index in range(len(items))
    ]
    cells = []
    for index, item in enumerate(items):
        note = f'<div class="kit-kpi-note">{escape(item.note)}</div>' if item.note else ""
        cells.append(
            f'<div class="kit-kpi" style="background:{tints[index]}"><div class="kit-kpi-top"><span>{escape(item.label)}</span></div>'
            f'<div class="kit-kpi-value">{escape(item.value)}</div>{note}</div>'
        )
    # Até cinco por linha; seis viram duas linhas de três, sem cartão sozinho no fim.
    columns = len(items) if len(items) <= 5 else 3 if len(items) % 3 == 0 else 4
    st.html(f'<div class="kit-kpis" style="--kit-columns:{columns}">{"".join(cells)}</div>')


def progress(items: list[Progress], tokens: Tokens) -> None:
    """Nome e valor numa linha, barra fina embaixo: 'quanto do limite está em uso'."""
    rows = []
    for item in items:
        width = 0 if item.fraction is None else max(0.0, min(1.0, item.fraction)) * 100
        rows.append(
            f'<div><div class="kit-bar-top"><span>{escape(item.label)}</span><span>{escape(item.value)}</span></div>'
            f'<div class="kit-track"><div class="kit-fill" style="width:{width:.1f}%;background:{item.color or tokens.ink}"></div></div></div>'
        )
    st.html(f'<div class="kit-bars">{"".join(rows)}</div>')


def bar_list(labels, values, texts, tokens: Tokens, hover=None, colors=None) -> None:
    """Ranking escrito: nome inteiro (quebra linha, nunca corta), valor à direita e barra fina proporcional."""
    values = [float(value or 0) for value in values]
    largest = max([abs(value) for value in values] + [0]) or 1
    palette = colors if isinstance(colors, list) else [colors or tokens.series[0]] * len(values)
    items = []
    for index, (label, value, text) in enumerate(zip(labels, values, texts, strict=True)):
        tip = f' title="{escape(str(hover[index]))}"' if hover else ""
        items.append(
            f'<div{tip}><div class="kit-bar-top"><span>{escape(str(label))}</span><span>{escape(str(text))}</span></div>'
            f'<div class="kit-track"><div class="kit-fill" style="width:{abs(value) / largest * 100:.1f}%;background:{palette[index]}"></div></div></div>'
        )
    st.html(f'<div class="kit-bars">{"".join(items)}</div>')


def legend(labels, colors, texts) -> None:
    """Legenda em lista, como embaixo da rosca do design: bolinha, nome e valor."""
    rows = "".join(
        f'<div class="kit-legend-row"><span><span class="kit-dot" style="background:{color}"></span>{escape(str(label))}</span>'
        f"<span>{escape(str(text))}</span></div>"
        for label, color, text in zip(labels, colors, texts, strict=True)
    )
    st.html(f'<div class="kit-legend">{rows}</div>')


def pill(text: str, color: str) -> str:
    """Bolinha colorida e texto escuro; a cor só reforça o que o texto diz."""
    return f'<span class="kit-tag"><span class="kit-dot" style="background:{color}"></span>{escape(text)}</span>'


def rows(pairs: dict[str, str]) -> None:
    """Lista de 'nome ... valor', como uma ficha."""
    body = "".join(
        f"<div class='kit-row'><span>{escape(key)}</span><span>{escape(str(value))}</span></div>" for key, value in pairs.items()
    )
    st.html(f'<div class="kit-list">{body}</div>')


def note(text: str) -> None:
    st.html(f'<div class="kit-note">{escape(text)}</div>')


def empty(text: str) -> None:
    st.html(f'<div class="kit-empty">{escape(text)}</div>')


def chart(figure: go.Figure, height: int = 300, key: str | None = None) -> None:
    figure.update_layout(height=height)
    st.plotly_chart(figure, theme=None, width="stretch", key=key, config={"displaylogo": False, "displayModeBar": False})


def percent_or_dash(fraction) -> str:
    return formatting.percent(fraction) if fraction is not None else formatting.MISSING
