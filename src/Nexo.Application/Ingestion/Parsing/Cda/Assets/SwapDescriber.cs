using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Bloco 3: contratos de swap.</summary>
internal sealed class SwapDescriber : IAssetDescriber
{
    public IReadOnlyList<string> DetailColumns { get; } = [];

    public AssetRecord Describe(RowReader row, string applicationType, string? assetType)
    {
        var code = row.Text("CD_SWAP");
        var description = row.Text("DS_SWAP");

        return new AssetRecord(AssetIdentity.ForSwap(code, description), CdaBlock.Swaps)
        {
            ApplicationType = applicationType,
            AssetType = assetType,
            Ticker = code,
            Description = description ?? code,
        };
    }
}
