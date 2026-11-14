using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Bloco 8: o que não tem código, como caixa, valores a pagar e receber, futuros e opções.</summary>
internal sealed class OtherAssetDescriber : IAssetDescriber
{
    public IReadOnlyList<string> DetailColumns { get; } = [];

    public AssetRecord Describe(RowReader row, string applicationType, string? assetType)
    {
        var description = row.Text("DS_ATIVO");
        var issuer = Issuer.Read(row, "CPF_CNPJ_EMISSOR", "EMISSOR", "PF_PJ_EMISSOR");

        return new AssetRecord(AssetIdentity.ForOther(applicationType, assetType, description ?? issuer.Name), CdaBlock.Other)
        {
            ApplicationType = applicationType,
            AssetType = assetType,
            Description = description ?? issuer.Name,
            IssuerDocument = issuer.Document,
            IssuerName = issuer.Name,
        };
    }
}
