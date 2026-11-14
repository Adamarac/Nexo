"""Transforma uma tabela em arquivo para baixar, pronto para abrir no Excel brasileiro."""

import io

import pandas as pd


def to_csv(table: pd.DataFrame) -> bytes:
    # Ponto e vírgula e vírgula decimal: o Excel em português abre sem pedir configuração.
    text = table.to_csv(index=False, sep=";", decimal=",", date_format="%d/%m/%Y")
    return text.encode("utf-8-sig")


def to_excel(table: pd.DataFrame, sheet_name: str = "Nexo") -> bytes:
    buffer = io.BytesIO()
    with pd.ExcelWriter(buffer, engine="openpyxl") as writer:
        table.to_excel(writer, index=False, sheet_name=sheet_name[:31])
    return buffer.getvalue()
