using Nexo.Application.Ingestion.Parsing.Cda.Assets;
using Nexo.Domain.Common;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Arquivos cda_fi_BLC_1 a BLC_8. A parte comum é igual; o ativo depende do bloco.</summary>
internal sealed class BlockFileParser(CdaBlock block, IAssetDescriber describer, ReferenceMonth month) : ICdaFileParser
{
    public IReadOnlyCollection<string> RequiredColumns => CommonPositionFields.PositionColumns;

    public ParsedCdaRow Parse(CsvRow row, RowOrigin origin)
    {
        var reader = new RowReader(row);
        var fund = CommonPositionFields.ReadFund(reader, month);
        var applicationType = reader.RequiredText("TP_APLIC");
        var assetType = reader.Text("TP_ATIVO");
        var amounts = CommonPositionFields.ReadAmounts(reader);
        var confidentialUntil = reader.Date("DT_CONFID_APLIC");
        var asset = describer.Describe(reader, applicationType, assetType);

        if (reader.Rejection is { } rejection)
            return ParsedCdaRow.Rejected(rejection);

        return ParsedCdaRow.Of(new PositionRecord(fund, applicationType, amounts, origin)
        {
            Block = block,
            AssetType = assetType,
            TradingIntent = reader.Text("TP_NEGOC"),
            IssuerIsRelated = reader.YesNo("EMISSOR_LIGADO"),
            ConfidentialUntil = confidentialUntil,
            Asset = asset,
            DetailsJson = DetailsJson.From(row, describer.DetailColumns),
        });
    }
}
