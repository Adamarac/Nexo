using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Bloco 5: depósitos e títulos de bancos, como CDB, LF e DPGE.</summary>
internal sealed class BankDepositDescriber : IAssetDescriber
{
    public IReadOnlyList<string> DetailColumns { get; } =
    [
        "TITULO_POSFX", "CD_INDEXADOR_POSFX", "DS_INDEXADOR_POSFX", "PR_INDEXADOR_POSFX", "PR_CUPOM_POSFX",
        "PR_TAXA_PREFX", "RISCO_EMISSOR", "AG_RISCO", "DT_RISCO", "GRAU_RISCO",
    ];

    // Esses títulos não têm código de papel, então o ativo é identificado por banco, tipo, vencimento e indexador.
    public AssetRecord Describe(RowReader row, string applicationType, string? assetType)
    {
        var issuer = Issuer.Read(row, "CNPJ_EMISSOR", "EMISSOR");
        var maturity = row.Date("DT_VENC");
        var indexer = row.Text("CD_INDEXADOR_POSFX");

        return new AssetRecord(AssetIdentity.ForBankDeposit(issuer.DocumentOrName, assetType, maturity, indexer), CdaBlock.BankDeposits)
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
