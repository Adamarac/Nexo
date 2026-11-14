using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Bloco 4: ativos com código de negociação, como ações, debêntures, BDRs e opções.</summary>
internal sealed class ListedAssetDescriber : IAssetDescriber
{
    public IReadOnlyList<string> DetailColumns { get; } = ["DT_INI_VIGENCIA", "DT_FIM_VIGENCIA"];

    public AssetRecord Describe(RowReader row, string applicationType, string? assetType)
    {
        var ticker = row.Text("CD_ATIVO");
        var isin = row.Text("CD_ISIN");
        var description = row.Text("DS_ATIVO");

        // O ISIN é o código mais estável; o ticker fica de reserva para quando ele não vem.
        var identity = (isin, ticker) switch
        {
            ({ } code, _) => AssetIdentity.ForIsin(code),
            (null, { } symbol) => AssetIdentity.ForTicker(symbol),
            _ => AssetIdentity.ForOther(applicationType, assetType, description),
        };

        return new AssetRecord(identity, CdaBlock.ListedAssets)
        {
            ApplicationType = applicationType,
            AssetType = assetType,
            Isin = isin,
            Ticker = ticker,
            Description = description,
        };
    }
}
