from datetime import date

import pandas as pd

from analytics import ui


def test_tidy_fills_text_and_converts_numbers_and_dates():
    frame = pd.DataFrame(
        {
            "ISIN": ["BRSTNCLF1RH3", None],
            "Quantidade": pd.Series([16089236.0, None], dtype=object),
            "Última carteira": pd.Series([date(2026, 10, 31), None], dtype=object),
        }
    )
    tidy = ui.tidy(frame)
    assert tidy["ISIN"].tolist() == ["BRSTNCLF1RH3", "—"]
    assert pd.api.types.is_numeric_dtype(tidy["Quantidade"])
    assert pd.api.types.is_datetime64_any_dtype(tidy["Última carteira"])


def test_money_scale_picks_a_readable_unit():
    assert ui.money_scale(345_578_879) == (1e6, "R$ mi")
    assert ui.money_scale(16_681_769_901_785) == (1e12, "R$ tri")
    assert ui.money_scale(850) == (1.0, "R$")
