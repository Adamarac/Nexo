using Nexo.Domain.Common;
using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Colunas que se repetem em todos os arquivos de carteira.</summary>
internal static class CommonPositionFields
{
    public static readonly IReadOnlyCollection<string> FundColumns =
        ["TP_FUNDO_CLASSE", "CNPJ_FUNDO_CLASSE", "DENOM_SOCIAL", "DT_COMPTC"];

    public static readonly IReadOnlyCollection<string> PositionColumns = [.. FundColumns, "TP_APLIC"];

    public static ReportingFund ReadFund(RowReader row, ReferenceMonth month)
    {
        var cnpj = row.RequiredCnpj("CNPJ_FUNDO_CLASSE");
        var name = row.RequiredText("DENOM_SOCIAL");
        var referenceDate = row.RequiredDate("DT_COMPTC");

        if (!row.HasFailed && !month.Contains(referenceDate))
            row.Reject(RejectionReason.ReferenceDateMismatch, $"A data {referenceDate:yyyy-MM-dd} não pertence ao mês {month}.");

        return new ReportingFund(cnpj, name, row.Text("TP_FUNDO_CLASSE"), referenceDate);
    }

    public static PositionAmounts ReadAmounts(RowReader row) => new(
        Quantity: row.Decimal("QT_POS_FINAL"),
        MarketValue: row.Decimal("VL_MERC_POS_FINAL"),
        CostValue: row.Decimal("VL_CUSTO_POS_FINAL"),
        BoughtQuantity: row.Decimal("QT_AQUIS_NEGOC"),
        BoughtValue: row.Decimal("VL_AQUIS_NEGOC"),
        SoldQuantity: row.Decimal("QT_VENDA_NEGOC"),
        SoldValue: row.Decimal("VL_VENDA_NEGOC"));
}
