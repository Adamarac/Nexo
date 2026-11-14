"""Gráficos comuns às duas telas, no estilo do design de referência: sem grade, traços finos, barras com
cantos arredondados e rótulos pequenos em cinza. Nomes longos nunca vão para um eixo do Plotly."""

import pandas as pd
import plotly.graph_objects as go
import plotly.io as pio

from nexo_shared.ui_kit import Tokens


def register_template(name: str, tokens: Tokens) -> None:
    axis = dict(
        showgrid=False,
        zeroline=False,
        showline=False,
        ticks="",
        automargin=True,
        tickfont=dict(color=tokens.muted, size=12),
        title=dict(font=dict(size=12, color=tokens.muted)),
        # Afasta os números dos eixos para o "0" do eixo vertical não encostar no primeiro rótulo de baixo.
        ticklabelstandoff=10,
    )
    pio.templates[name] = go.layout.Template(
        layout=go.Layout(
            paper_bgcolor="rgba(0,0,0,0)",
            plot_bgcolor="rgba(0,0,0,0)",
            font=dict(family=tokens.font, size=12, color=tokens.text),
            colorway=tokens.series,
            xaxis=axis,
            yaxis=axis,
            margin=dict(l=4, r=8, t=8, b=4),
            legend=dict(orientation="h", x=0, xanchor="left", y=-0.2, yanchor="top", font=dict(size=12), title_text=""),
            hoverlabel=dict(bgcolor="#ffffff", bordercolor=tokens.line, font=dict(family=tokens.font, color=tokens.text, size=12)),
            separators=",.",
            bargap=0.45,
            barcornerradius=6,
        ),
        data=dict(scatter=[go.Scatter(line=dict(width=1.75, shape="spline", smoothing=0.6), marker=dict(size=6))]),
    )
    pio.templates.default = name


def short(text, limit: int = 26) -> str:
    """Encurta só onde não há espaço (legendas do Plotly). Um complemento depois de ' · ' é preservado."""
    text = str(text)
    if len(text) <= limit:
        return text
    name, separator, extra = text.rpartition(" · ")
    if separator and len(extra) < limit - 6:
        return name[: limit - len(extra) - 4] + "… · " + extra
    return text[: limit - 1] + "…"


def distinct(labels: list[str], extras: list) -> list[str]:
    """Nomes repetidos ganham um complemento (ISIN, vencimento) para o leitor saber que são itens diferentes."""
    counts = pd.Series(labels).value_counts()
    return [f"{label} · {extra}" if counts[label] > 1 and extra else label for label, extra in zip(labels, extras, strict=True)]


def donut(values: list[float], colors: list[str], center: str, surface: str) -> go.Figure:
    """Rosca como a do design: fatias separadas por um respiro da cor do cartão e um número no meio."""
    figure = go.Figure(
        go.Pie(
            values=values,
            hole=0.62,
            sort=False,
            direction="clockwise",
            textinfo="none",
            hoverinfo="skip",
            marker=dict(colors=colors, line=dict(color=surface, width=3)),
        )
    )
    figure.update_layout(
        showlegend=False,
        margin=dict(l=0, r=0, t=0, b=0),
        annotations=[dict(text=f"<b>{center}</b>", showarrow=False, font=dict(size=18))],
    )
    return figure


def stacked_shares(
    rows: list[str], categories: list[str], shares: dict[str, list[float]], colors: dict[str, str], surface: str
) -> go.Figure:
    """Uma barra 100% por linha (por exemplo, um fundo), dividida pelas categorias, com respiro entre as partes."""
    figure = go.Figure()
    for category in categories:
        values = [share * 100 for share in shares[category]]
        figure.add_bar(
            y=rows,
            x=values,
            name=category,
            orientation="h",
            marker=dict(color=colors[category], line=dict(color=surface, width=2)),
            text=[f"{value:.0f}%" if value >= 10 else "" for value in values],
            textposition="inside",
            insidetextanchor="middle",
            textfont=dict(color="#1C1C1C", size=12),
            hovertemplate=f"{category}<br>%{{x:.1f}}%<extra>%{{y}}</extra>",
        )
    figure.update_layout(barmode="stack", bargap=0.4, barcornerradius=0, legend=dict(y=-0.15))
    figure.update_xaxes(range=[0, 100], ticksuffix="%")
    figure.update_yaxes(autorange="reversed")
    return figure


def ring(fraction: float, color: str, track: str, label: str) -> go.Figure:
    """Anel com um único percentual no meio, como um medidor."""
    fraction = max(0.0, min(1.0, fraction))
    figure = go.Figure(
        go.Pie(
            values=[fraction, 1 - fraction],
            hole=0.78,
            sort=False,
            direction="clockwise",
            marker=dict(colors=[color, track]),
            textinfo="none",
            hoverinfo="skip",
        )
    )
    figure.update_layout(
        showlegend=False, margin=dict(l=0, r=0, t=0, b=0), annotations=[dict(text=f"<b>{label}</b>", showarrow=False, font=dict(size=20))]
    )
    return figure


def color_map(names, palette: list[str]) -> dict[str, str]:
    """A cor acompanha o nome, sempre na mesma ordem alfabética."""
    return {name: palette[index % len(palette)] for index, name in enumerate(sorted({str(name) for name in names}))}
