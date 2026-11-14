import pytest

from analytics import metrics

HISTORY = [
    {"referenceDate": "2025-01-31", "netWorth": 100.0, "fundClassId": 1},
    {"referenceDate": "2025-02-28", "netWorth": 120.0, "fundClassId": 1},
    {"referenceDate": "2025-02-28", "netWorth": 5.0, "fundClassId": 2},
    {"referenceDate": "2025-03-31", "netWorth": 90.0, "fundClassId": 1},
    {"referenceDate": "2026-01-31", "netWorth": 150.0, "fundClassId": 1},
]


def test_series_keeps_the_largest_class_per_month():
    series = metrics.net_worth_series(HISTORY)
    assert series.tolist() == [100.0, 120.0, 90.0, 150.0]


def test_summary_measures():
    summary = metrics.summarize(metrics.net_worth_series(HISTORY))
    assert summary.months == 4
    assert summary.change_month == pytest.approx(150 / 90 - 1)
    assert summary.change_12m == pytest.approx(150 / 100 - 1)
    assert summary.change_total == pytest.approx(0.5)
    assert summary.best_change == pytest.approx(150 / 90 - 1)
    assert summary.worst_change == pytest.approx(-0.25)
    assert summary.max_drawdown == pytest.approx(-0.25)


def test_summary_of_empty_history():
    assert metrics.summarize(metrics.net_worth_series([])).months == 0


POSITIONS = [
    {"assetId": 1, "marketValue": 60.0, "issuerName": "Tesouro", "assetDescription": "LFT"},
    {"assetId": 2, "marketValue": 30.0, "issuerName": "Banco A", "assetDescription": "CDB"},
    {"assetId": 2, "marketValue": 10.0, "issuerName": "Banco A", "assetDescription": "CDB"},
    {"assetId": None, "marketValue": 50.0, "issuerName": None, "assetDescription": None},
    {"assetId": 3, "marketValue": -5.0, "issuerName": None, "assetDescription": "Taxa"},
]


def test_weights_ignore_confidential_and_negative_positions():
    weights = metrics.weights(POSITIONS)
    assert weights.to_dict() == {1: 0.6, 2: 0.4}


def test_concentration_measures():
    result = metrics.concentration(metrics.weights(POSITIONS))
    assert result.top1 == pytest.approx(0.6)
    assert result.hhi == pytest.approx(0.36 + 0.16)
    assert result.effective_count == pytest.approx(1 / 0.52)


def test_concentration_curve_accumulates():
    curve = metrics.concentration_curve(metrics.weights(POSITIONS))
    assert curve["acumulado"].tolist() == pytest.approx([0.6, 1.0])


def test_overlap_sums_the_smaller_weight_of_each_shared_asset():
    first = metrics.weights(POSITIONS)
    second = metrics.weights([{"assetId": 2, "marketValue": 80.0}, {"assetId": 9, "marketValue": 20.0}])
    assert metrics.overlap(first, second) == (1, pytest.approx(0.4))


def test_issuers_group_positive_positions():
    issuers = metrics.by_issuer(POSITIONS, net_worth=200.0)
    assert issuers["emissor"].tolist()[:2] == ["Tesouro", "Banco A"]
    assert issuers.loc[1, "valor"] == 40.0
    assert issuers.loc[0, "participação"] == pytest.approx(0.3)
