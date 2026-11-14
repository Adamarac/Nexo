"""Nomes em português para os códigos que a API devolve."""

from nexo_shared import formatting

# Blocos do informe da CVM. Posição sem bloco é a parte que o fundo mantém em sigilo.
BLOCKS = {
    0: "Fundos estruturados",
    1: "Títulos públicos",
    2: "Cotas de fundos",
    3: "Swap",
    4: "Ações, debêntures e outros códigos",
    5: "Depósitos e títulos bancários",
    6: "Agronegócio e crédito privado",
    7: "Exterior",
    8: "Outros (caixa, valores a pagar e receber)",
}
CONFIDENTIAL = "Sigiloso"

FILE_STATUSES = {
    "Downloaded": "Baixado",
    "Processing": "Processando",
    "Processed": "Processado",
    "Failed": "Falhou",
    "Superseded": "Substituído",
}

FILE_KINDS = {
    "CdaMonthly": "Carteira mensal",
    "FundRegistry": "Cadastro de fundos",
}

REJECTION_REASONS = {
    "FieldCountMismatch": "Quantidade de campos errada",
    "InvalidCnpj": "CNPJ inválido",
    "InvalidDate": "Data inválida",
    "InvalidNumber": "Número inválido",
    "MissingRequiredValue": "Campo obrigatório vazio",
    "ReferenceDateMismatch": "Data de outro mês",
    "DuplicateRow": "Linha repetida",
    "InconsistentNetWorth": "PL diferente no mesmo fundo",
}

FUND_TYPES = ["FI", "FIF", "FIDC", "FIAGRO", "FIP", "FII", "FIIM", "FACFIF"]


def block(value) -> str:
    if value is None or value != value:
        return CONFIDENTIAL
    return BLOCKS.get(int(value), f"Bloco {int(value)}")


def file_status(value) -> str:
    return FILE_STATUSES.get(value, value or formatting.MISSING)


def file_kind(value) -> str:
    return FILE_KINDS.get(value, value or formatting.MISSING)


def rejection_reason(value) -> str:
    return REJECTION_REASONS.get(value, value or formatting.MISSING)


def asset(ticker=None, description=None, maturity_date=None) -> str:
    """Nome curto do ativo: o ticker quando existe; senão a descrição, com o vencimento se houver."""
    if _present(ticker):
        return str(ticker)
    name = str(description) if _present(description) else "Ativo sem descrição"
    if _present(maturity_date):
        return f"{name} (venc. {formatting.day(maturity_date)})"
    return name


def maturities(count: int, first=None, last=None) -> str:
    """Vencimentos de um grupo: quantos e o intervalo; com um só, a data dele."""
    if count > 1:
        return f"{formatting.number(count)} vencimentos, de {formatting.month(first)} a {formatting.month(last)}"
    return formatting.day(first) if _present(first) else "sem vencimento"


def _present(value) -> bool:
    """Vazio, None e NaN (o pandas 3 usa NaN para texto ausente) contam como ausentes."""
    return value is not None and value == value and str(value).strip() != ""


# Situações do cadastro da CVM, como aparecem nos dados; o filtro da API compara o texto exato.
FUND_SITUATIONS = [
    "Em Funcionamento Normal",
    "Fase Pré-Operacional",
    "Em Liquidação",
    "Em Situação Especial",
    "Incorporação",
    "Em Análise",
    "Cancelado",
]
