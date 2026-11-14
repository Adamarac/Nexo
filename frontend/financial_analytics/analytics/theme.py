"""Cores da Análise, as mesmas do DESIGN.md (estilo do design de referência)."""

from nexo_shared import viz
from nexo_shared.ui_kit import Tokens

INK = "#1C1C1C"
# Paleta categórica validada para daltonismo contra o cinza dos cartões. A ordem é fixa.
SERIES = ["#7B8CF8", "#4CC38A", "#F0A04B", "#3AA5EE", "#E57BA9"]
ACCENT = SERIES[0]
NEGATIVE = SERIES[2]
OTHER = "#C4CAD4"
COMPARE = SERIES[:4]

TOKENS = Tokens(
    page="#FFFFFF",
    card="#F7F9FB",
    tint_a="#E3F5FF",
    tint_b="#E5ECF6",
    line="#E6E8EC",
    text=INK,
    muted="#6B7280",
    ink=INK,
    series=SERIES,
    other=OTHER,
)
TRACK = TOKENS.line
TEMPLATE = "nexo_analytics"

viz.register_template(TEMPLATE, TOKENS)
