using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Bloco 7: investimentos no exterior. Boa parte vem em texto livre.</summary>
internal sealed class ForeignAssetDescriber : IAssetDescriber
{
    public IReadOnlyList<string> DetailColumns { get; } =
    [
        "INVEST_COLETIVO", "INVEST_COLETIVO_GESTOR", "PAIS", "CD_BV_MERC", "BV_MERC", "RISCO_EMISSOR",
        "AG_RISCO", "DT_RISCO", "GRAU_RISCO", "QT_ATIVO_EXTERIOR", "VL_ATIVO_EXTERIOR",
    ];

    public AssetRecord Describe(RowReader row, string applicationType, string? assetType)
    {
        var country = row.Text("CD_PAIS");
        var exchangeCode = row.Text("CD_ATIVO_BV_MERC");
        var issuerName = row.Text("EMISSOR");
        var description = row.Text("DS_ATIVO_EXTERIOR");

        return new AssetRecord(AssetIdentity.ForForeign(country, exchangeCode ?? issuerName, description), CdaBlock.Foreign)
        {
            ApplicationType = applicationType,
            AssetType = assetType,
            Ticker = exchangeCode,
            Description = description ?? issuerName,
            IssuerName = issuerName,
            CountryCode = country,
            MaturityDate = row.Date("DT_VENC"),
        };
    }
}
