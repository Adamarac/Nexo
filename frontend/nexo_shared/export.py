"""Transforma uma tabela em arquivo para baixar, pronto para abrir no Excel brasileiro."""

import io

import pandas as pd
from openpyxl.cell.cell import ILLEGAL_CHARACTERS_RE


def to_csv(table: pd.DataFrame) -> bytes:
    # Ponto e vírgula e vírgula decimal: o Excel em português abre sem pedir configuração.
    text = table.to_csv(index=False, sep=";", decimal=",", date_format="%d/%m/%Y")
    return text.encode("utf-8-sig")


def to_excel(table: pd.DataFrame, sheet_name: str = "Nexo") -> bytes:
    buffer = io.BytesIO()
    with pd.ExcelWriter(buffer, engine="openpyxl") as writer:
        _without_control_characters(table).to_excel(writer, index=False, sheet_name=sheet_name[:31])
    return buffer.getvalue()


def _without_control_characters(table: pd.DataFrame) -> pd.DataFrame:
    # Alguns nomes chegam da CVM com caracteres de controle, que o Excel recusa; eles não têm significado.
    clean = table.copy()
    for column in clean.columns:
        if clean[column].dtype == object or pd.api.types.is_string_dtype(clean[column]):
            clean[column] = clean[column].map(lambda value: ILLEGAL_CHARACTERS_RE.sub("", value) if isinstance(value, str) else value)
    return clean
