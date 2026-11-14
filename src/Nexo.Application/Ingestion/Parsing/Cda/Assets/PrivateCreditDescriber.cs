using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Bloco 6: títulos do agronegócio e de crédito privado, como CRA, CCB e notas promissórias.</summary>
internal sealed class PrivateCreditDescriber : IAssetDescriber
{
    public IReadOnlyList<string> DetailColumns { get; } =
    [
        "TITULO_POSFX", "CD_INDEXADOR_POSFX", "DS_INDEXADOR_POSFX", "PR_INDEXADOR_POSFX", "PR_CUPOM_POSFX",
        "PR_TAXA_PREFX", "TITULO_CETIP", "TITULO_GARANTIA", "CNPJ_INSTITUICAO_FINANC_COOBR",
    ];

    public AssetRecord Describe(RowReader row, string applicationType, string? assetType)
    {
        var issuer = Issuer.Read(row, "CPF_CNPJ_EMISSOR", "EMISSOR", "PF_PJ_EMISSOR");
        var maturity = row.Date("DT_VENC");

        return new AssetRecord(AssetIdentity.ForPrivateCredit(issuer.DocumentOrName, assetType, maturity), CdaBlock.AgribusinessAndCredit)
        {
            ApplicationType = applicationType,
            AssetType = assetType,
            Description = $"{assetType} {issuer.Name}".Trim(),
            IssuerDocument = issuer.Document,
            IssuerName = issuer.Name,
            MaturityDate = maturity,
        };
    }
}
