import io

import pandas as pd
from openpyxl import load_workbook

from nexo_shared import export, labels


def test_block_without_number_is_confidential():
    assert labels.block(None) == "Sigiloso"
    assert labels.block(1) == "Títulos públicos"
    assert labels.block(42) == "Bloco 42"


def test_known_codes_are_translated_and_unknown_pass_through():
    assert labels.file_status("Failed") == "Falhou"
    assert labels.file_status("Novo") == "Novo"
    assert labels.file_kind("FundRegistry") == "Cadastro de fundos"
    assert labels.rejection_reason("InvalidCnpj") == "CNPJ inválido"


def test_asset_label_prefers_ticker_then_description_with_maturity():
    assert labels.asset("PETR4", "PETROBRAS PN") == "PETR4"
    assert labels.asset(None, "LETRAS FINANCEIRAS DO TESOURO", "2027-09-01") == "LETRAS FINANCEIRAS DO TESOURO (venc. 01/09/2027)"
    assert labels.asset(None, None) == "Ativo sem descrição"


def test_csv_opens_in_brazilian_excel():
    content = export.to_csv(pd.DataFrame({"Fundo": ["Ação"], "Valor": [1234.5]}))
    assert content.startswith("﻿".encode())
    assert content.decode("utf-8-sig").splitlines() == ["Fundo;Valor", "Ação;1234,5"]


def test_excel_keeps_numbers_as_numbers():
    content = export.to_excel(pd.DataFrame({"Valor": [1234.5]}), sheet_name="Carteira")
    sheet = load_workbook(io.BytesIO(content))["Carteira"]
    assert sheet["A2"].value == 1234.5


def test_excel_drops_control_characters_that_come_in_cvm_names():
    # A CVM às vezes manda nomes com caracteres de controle, que o Excel não aceita.
    name = "EMIVE PARTICIPA" + chr(3) + chr(19) + "ES S.A."
    content = export.to_excel(pd.DataFrame({"Emissor": [name], "Fundos": [2]}))
    sheet = load_workbook(io.BytesIO(content))["Nexo"]
    assert sheet["A2"].value == "EMIVE PARTICIPAES S.A."
    assert sheet["B2"].value == 2


def test_asset_label_treats_nan_as_missing():
    nan = float("nan")
    assert labels.asset(nan, "LETRAS FINANCEIRAS DO TESOURO", "2027-09-01") == "LETRAS FINANCEIRAS DO TESOURO (venc. 01/09/2027)"
    assert labels.asset(nan, nan, nan) == "Ativo sem descrição"


def test_maturities_summarize_a_group_or_show_the_single_date():
    assert labels.maturities(16, "2027-09-01", "2032-03-01") == "16 vencimentos, de set/2027 a mar/2032"
    assert labels.maturities(1494, "2023-01-02", "2031-04-01") == "1.494 vencimentos, de jan/2023 a abr/2031"
    assert labels.maturities(1, "2027-09-01", "2027-09-01") == "01/09/2027"
    assert labels.maturities(1, None, None) == "sem vencimento"
