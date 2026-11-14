using Nexo.Domain.Common;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Arquivo cda_fi_PL: o patrimônio líquido de cada fundo na data.</summary>
internal sealed class NetWorthFileParser(ReferenceMonth month) : ICdaFileParser
{
    public IReadOnlyCollection<string> RequiredColumns { get; } = [.. CommonPositionFields.FundColumns, "VL_PATRIM_LIQ"];

    public ParsedCdaRow Parse(CsvRow row, RowOrigin origin)
    {
        var reader = new RowReader(row);
        var fund = CommonPositionFields.ReadFund(reader, month);
        var netWorth = reader.Decimal("VL_PATRIM_LIQ");

        return reader.Rejection is { } rejection
            ? ParsedCdaRow.Rejected(rejection)
            : ParsedCdaRow.Of(new NetWorthRecord(fund, netWorth, origin));
    }
}
