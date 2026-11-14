using Nexo.Domain.Common;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Arquivos de posições sigilosas: só o valor somado por tipo de aplicação, sem o ativo.</summary>
internal sealed class ConfidentialFileParser(ReferenceMonth month) : ICdaFileParser
{
    public IReadOnlyCollection<string> RequiredColumns => CommonPositionFields.PositionColumns;

    public ParsedCdaRow Parse(CsvRow row, RowOrigin origin)
    {
        var reader = new RowReader(row);
        var fund = CommonPositionFields.ReadFund(reader, month);
        var applicationType = reader.RequiredText("TP_APLIC");
        var amounts = CommonPositionFields.ReadAmounts(reader);
        var confidentialUntil = reader.Date("DT_CONFID_APLIC");

        if (reader.Rejection is { } rejection)
            return ParsedCdaRow.Rejected(rejection);

        return ParsedCdaRow.Of(new PositionRecord(fund, applicationType, amounts, origin)
        {
            IsConfidential = true,
            ConfidentialUntil = confidentialUntil,
        });
    }
}
