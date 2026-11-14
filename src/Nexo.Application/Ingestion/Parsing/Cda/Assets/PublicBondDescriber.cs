using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Bloco 1: títulos públicos registrados no SELIC.</summary>
internal sealed class PublicBondDescriber : IAssetDescriber
{
    public IReadOnlyList<string> DetailColumns { get; } = ["DT_EMISSAO"];

    public AssetRecord Describe(RowReader row, string applicationType, string? assetType)
    {
        var isin = row.Text("CD_ISIN");
        var selicCode = row.Text("CD_SELIC");
        var bondType = row.Text("TP_TITPUB");
        var maturity = row.Date("DT_VENC");

        var identity = (isin, selicCode) switch
        {
            ({ } code, _) => AssetIdentity.ForIsin(code),
            (null, { } selic) => AssetIdentity.ForSelic(selic, maturity),
            _ => AssetIdentity.ForOther(applicationType, assetType, bondType),
        };

        return new AssetRecord(identity, CdaBlock.PublicBonds)
        {
            ApplicationType = applicationType,
            AssetType = assetType,
            // O código SELIC (ex.: 210100) é o tipo do título, não um código de negociação; por isso não vai em Ticker.
            Isin = isin,
            Description = bondType,
            MaturityDate = maturity,
        };
    }
}
