"""Números, datas e tamanhos no jeito brasileiro: 1.234,56 e 31/10/2026."""

import math
from datetime import UTC, date, datetime
from zoneinfo import ZoneInfo

BRAZIL = ZoneInfo("America/Sao_Paulo")
MISSING = "—"
MONTHS = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"]


def _is_missing(value) -> bool:
    return value is None or (isinstance(value, float) and math.isnan(value))


def number(value, decimals: int = 0) -> str:
    if _is_missing(value):
        return MISSING
    # Formata no padrão americano e troca os separadores de lugar.
    text = f"{float(value):,.{decimals}f}"
    return text.replace(",", "_").replace(".", ",").replace("_", ".")


def percent(fraction, decimals: int = 1) -> str:
    if _is_missing(fraction):
        return MISSING
    return f"{number(float(fraction) * 100, decimals)}%"


_SCALES = [(1e12, "tri"), (1e9, "bi"), (1e6, "mi"), (1e3, "mil")]


def compact(value, decimals: int = 1) -> str:
    """1.234.567 vira '1,2 mi'. Bom para títulos e eixos; a tabela mostra o valor inteiro."""
    if _is_missing(value):
        return MISSING
    for size, suffix in _SCALES:
        if abs(value) >= size:
            return f"{number(value / size, decimals)} {suffix}"
    return number(value, 0)


def money(value, decimals: int = 2) -> str:
    return MISSING if _is_missing(value) else f"R$ {number(value, decimals)}"


def money_compact(value) -> str:
    return MISSING if _is_missing(value) else f"R$ {compact(value)}"


def size(byte_count) -> str:
    if _is_missing(byte_count):
        return MISSING
    value = float(byte_count)
    for unit in ["B", "KB", "MB", "GB", "TB"]:
        if abs(value) < 1024 or unit == "TB":
            return f"{number(value, 0 if unit == 'B' else 1)} {unit}"
        value /= 1024
    return MISSING


def duration(seconds) -> str:
    if _is_missing(seconds):
        return MISSING
    seconds = float(seconds)
    if seconds < 1:
        return f"{number(seconds * 1000, 0)} ms"
    if seconds < 60:
        return f"{number(seconds, 1)} s"
    minutes, rest = divmod(round(seconds), 60)
    if minutes < 60:
        return f"{minutes} min {rest:02d} s"
    hours, minutes = divmod(minutes, 60)
    if hours < 48:
        return f"{hours} h {minutes:02d} min"
    return f"{hours // 24} d {hours % 24} h"


def parse_date(value) -> date | None:
    if _is_missing(value) or value == "":
        return None
    if isinstance(value, datetime):
        return value.date()
    if isinstance(value, date):
        return value
    return date.fromisoformat(str(value)[:10])


def parse_moment(value) -> datetime | None:
    if _is_missing(value) or value == "":
        return None
    moment = value if isinstance(value, datetime) else datetime.fromisoformat(str(value).replace("Z", "+00:00"))
    return moment if moment.tzinfo else moment.replace(tzinfo=UTC)


def day(value) -> str:
    parsed = parse_date(value)
    return parsed.strftime("%d/%m/%Y") if parsed else MISSING


def month(value) -> str:
    """Data de referência mensal: '31/10/2026' vira 'out/2026'."""
    parsed = parse_date(value)
    return f"{MONTHS[parsed.month - 1]}/{parsed.year}" if parsed else MISSING


def moment(value, *, seconds: bool = False) -> str:
    """Data e hora no horário de Brasília."""
    parsed = parse_moment(value)
    if parsed is None:
        return MISSING
    pattern = "%d/%m/%Y %H:%M:%S" if seconds else "%d/%m/%Y %H:%M"
    return parsed.astimezone(BRAZIL).strftime(pattern)


def ago(value, now: datetime | None = None) -> str:
    """Quanto tempo atrás, só na maior unidade: 'há 3 min', 'há 2 h'."""
    parsed = parse_moment(value)
    if parsed is None:
        return MISSING
    elapsed = ((now or datetime.now(UTC)) - parsed).total_seconds()
    if elapsed < 1:
        return "agora"
    for size, unit in [(86_400, "d"), (3_600, "h"), (60, "min")]:
        if elapsed >= size:
            return f"há {int(elapsed // size)} {unit}"
    return f"há {int(elapsed)} s"


def cnpj(value) -> str:
    if _is_missing(value):
        return MISSING
    digits = "".join(char for char in str(value) if char.isdigit())
    if len(digits) != 14:
        return str(value)
    return f"{digits[:2]}.{digits[2:5]}.{digits[5:8]}/{digits[8:12]}-{digits[12:]}"
