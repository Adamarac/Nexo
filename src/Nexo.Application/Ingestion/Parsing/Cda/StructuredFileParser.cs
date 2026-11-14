using Nexo.Application.Ingestion.Parsing.Cda.Assets;
using Nexo.Domain.Common;
using Nexo.Domain.Ingestion;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Arquivo cda_fie dos fundos estruturados. O PL vem repetido em cada linha do fundo.</summary>
internal sealed class StructuredFileParser(ReferenceMonth month) : ICdaFileParser
{
    private readonly StructuredFundAssetDescriber _describer = new();
    private readonly Dictionary<Cnpj, decimal?> _netWorthByFund = [];

    public IReadOnlyCollection<string> RequiredColumns { get; } = [.. CommonPositionFields.PositionColumns, "VL_PATRIM_LIQ"];

    public ParsedCdaRow Parse(CsvRow row, RowOrigin origin)
    {
        var reader = new RowReader(row);
        var fund = CommonPositionFields.ReadFund(reader, month);
        var applicationType = reader.RequiredText("TP_APLIC");
        var assetType = reader.Text("TP_ATIVO");
        var amounts = CommonPositionFields.ReadAmounts(reader);
        var confidentialUntil = reader.Date("DT_CONFID_APLIC");
        var netWorth = reader.Decimal("VL_PATRIM_LIQ");
        var asset = _describer.Describe(reader, applicationType, assetType);

        if (reader.Rejection is { } rejection)
            return ParsedCdaRow.Rejected(rejection);

        if (HasDifferentNetWorth(fund.Cnpj, netWorth))
        {
            return ParsedCdaRow.Rejected(new RowRejection(row.LineNumber, RejectionReason.InconsistentNetWorth,
                $"O PL {netWorth} difere do informado em outra linha do mesmo fundo.", row.Raw));
        }

        var position = new PositionRecord(fund, applicationType, amounts, origin)
        {
            Block = CdaBlock.StructuredFunds,
            AssetType = assetType,
            TradingIntent = reader.Text("TP_NEGOC"),
            IssuerIsRelated = reader.YesNo("EMISSOR_LIGADO"),
            ConfidentialUntil = confidentialUntil,
            Asset = asset,
            DetailsJson = DetailsJson.From(row, _describer.DetailColumns),
        };

        return ParsedCdaRow.Of(position, FirstNetWorthOf(fund, netWorth, origin));
    }

    private bool HasDifferentNetWorth(Cnpj fund, decimal? netWorth) =>
        _netWorthByFund.TryGetValue(fund, out var known) && known is not null && netWorth is not null && known != netWorth;

    // O PL só é registrado na primeira linha de cada fundo; as outras repetem o mesmo número.
    private NetWorthRecord? FirstNetWorthOf(ReportingFund fund, decimal? netWorth, RowOrigin origin)
    {
        if (!_netWorthByFund.TryAdd(fund.Cnpj, netWorth))
            return null;

        var hash = RowHasher.Hash($"pl:{fund.Cnpj}:{fund.ReferenceDate:yyyy-MM-dd}:{netWorth}");
        return new NetWorthRecord(fund, netWorth, origin with { RowHash = hash });
    }
}
