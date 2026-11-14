using Nexo.Domain.Assets;
using Nexo.Domain.Common;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Bloco 2: cotas de outros fundos. O ativo é o próprio fundo investido.</summary>
internal sealed class FundShareDescriber : IAssetDescriber
{
    public IReadOnlyList<string> DetailColumns { get; } = ["ID_SUBCLASSE"];

    public AssetRecord Describe(RowReader row, string applicationType, string? assetType)
    {
        var fundDocument = TextNormalizer.NormalizeDocument(row.Text("CNPJ_FUNDO_CLASSE_COTA"));
        var subclassId = row.Text("ID_SUBCLASSE");
        var fundName = row.Text("NM_FUNDO_CLASSE_SUBCLASSE_COTA");

        var identity = fundDocument is null
            ? AssetIdentity.ForOther(applicationType, assetType, fundName)
            : AssetIdentity.ForFundShare(fundDocument, subclassId);

        return new AssetRecord(identity, CdaBlock.FundShares)
        {
            ApplicationType = applicationType,
            AssetType = assetType,
            Description = fundName,
            IssuerDocument = fundDocument,
            IssuerName = fundName,
            InvestedFundDocument = fundDocument,
        };
    }
}
