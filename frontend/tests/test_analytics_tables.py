from datetime import date

import pandas as pd

from analytics import charts, ui


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


def test_maturity_table_joins_the_month_holdings_to_each_maturity():
    assets = [
        {"id": 1, "maturityDate": "2027-09-01", "isin": "BRSTNCLF1RH3", "assetType": "Título público federal", "issuerName": None},
        {"id": 2, "maturityDate": "2023-03-01", "isin": "BRSTNCLF1R82", "assetType": "Título público federal", "issuerName": None},
    ]
    holdings = [{"assetId": 1, "funds": 1623, "marketValue": 251.1e9}]

    table = charts.maturity_table(assets, holdings)

    assert list(table["maturity"]) == [date(2027, 9, 1), date(2023, 3, 1)]
    assert table.loc[0, "funds"] == 1623
    assert table.loc[0, "value"] == 251.1e9
    # Vencimento que nenhum fundo tem no mês fica vazio, não zero: a tela mostra "—".
    assert pd.isna(table.loc[1, "funds"])
    assert pd.isna(table.loc[1, "value"])


def test_maturity_months_add_up_maturities_of_the_same_month_in_date_order():
    table = pd.DataFrame(
        {
            "maturity": [date(2027, 5, 20), date(2027, 3, 1), date(2027, 5, 3), date(2023, 1, 17)],
            "value": [10.0, 5.0, 30.0, float("nan")],
        }
    )

    months = charts.maturity_months(table)

    # Vencimento sem valor no mês fica de fora; os do mesmo mês viram uma barra só.
    assert list(months["month"]) == [date(2027, 3, 1), date(2027, 5, 1)]
    assert list(months["value"]) == [5.0, 40.0]
    assert list(months["maturities"]) == [1, 2]
