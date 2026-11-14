from datetime import UTC, datetime

import pytest

from nexo_shared import formatting


@pytest.mark.parametrize(
    ("value", "decimals", "expected"),
    [(1234567.891, 2, "1.234.567,89"), (0, 0, "0"), (-1500, 0, "-1.500"), (None, 0, "—"), (float("nan"), 0, "—")],
)
def test_number_uses_brazilian_separators(value, decimals, expected):
    assert formatting.number(value, decimals) == expected


def test_percent_receives_a_fraction():
    assert formatting.percent(0.5470488336) == "54,7%"


@pytest.mark.parametrize(
    ("value", "expected"),
    [(16_681_769_901_785.62, "16,7 tri"), (302_699_243_651, "302,7 bi"), (345_578_879, "345,6 mi"), (12_300, "12,3 mil"), (950, "950")],
)
def test_compact_picks_the_largest_scale(value, expected):
    assert formatting.compact(value) == expected


def test_money_formats():
    assert formatting.money(1178.43) == "R$ 1.178,43"
    assert formatting.money_compact(345_578_879.6) == "R$ 345,6 mi"


@pytest.mark.parametrize(("value", "expected"), [(512, "512 B"), (14_341_673, "13,7 MB"), (11 * 1024**3, "11,0 GB")])
def test_size_uses_binary_units(value, expected):
    assert formatting.size(value) == expected


@pytest.mark.parametrize(
    ("seconds", "expected"), [(0.25, "250 ms"), (8.44, "8,4 s"), (82, "1 min 22 s"), (3720, "1 h 02 min"), (200_000, "2 d 7 h")]
)
def test_duration(seconds, expected):
    assert formatting.duration(seconds) == expected


def test_dates_and_months():
    assert formatting.day("2026-10-31") == "31/10/2026"
    assert formatting.month("2026-10-31") == "out/2026"
    assert formatting.month(None) == "—"


def test_moment_is_shown_in_brasilia_time():
    assert formatting.moment("2026-11-14T07:20:13.122063+00:00") == "14/11/2026 04:20"


def test_ago_counts_from_now():
    now = datetime(2026, 11, 14, 8, 0, tzinfo=UTC)
    assert formatting.ago("2026-11-14T07:57:00+00:00", now=now) == "há 3 min"
    assert formatting.ago("2026-11-14T05:10:00+00:00", now=now) == "há 2 h"
    assert formatting.ago("2026-11-14T09:00:00+00:00", now=now) == "agora"


def test_cnpj_mask():
    assert formatting.cnpj("47525165000140") == "47.525.165/0001-40"
    assert formatting.cnpj("123") == "123"
