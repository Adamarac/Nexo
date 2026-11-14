using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Arquivo cda_fie (FIDC, FIAGRO, FIIM): todos os tipos de ativo numa tabela só.</summary>
/// <remarks>Como não há bloco, a identidade é escolhida pelo que a linha tem preenchido.</remarks>
internal sealed class StructuredFundAssetDescriber : IAssetDescriber
{
    private const string FundSharesPrefix = "Cotas de Fundos";

    public IReadOnlyList<string> DetailColumns { get; } =
        ["ID_DOC", "RISCO_EMISSOR", "DT_INI_VIGENCIA", "PAIS", "CD_BV_MERC", "BV_MERC"];

    public AssetRecord Describe(RowReader row, string applicationType, string? assetType)
    {
        var selicCode = row.Text("CD_SELIC");
        var ticker = row.Text("CD_ATIVO");
        var description = row.Text("DS_ATIVO");
        var maturity = row.Date("DT_VENC");
        var issuer = Issuer.Read(row, "CPF_CNPJ_EMISSOR", "EMISSOR", "PF_PJ_EMISSOR");
        var isFundShare = applicationType.StartsWith(FundSharesPrefix, StringComparison.OrdinalIgnoreCase);
        var identity = ChooseIdentity(applicationType, assetType, selicCode, ticker, description, maturity, issuer, isFundShare);

        return new AssetRecord(identity, CdaBlock.StructuredFunds)
        {
            ApplicationType = applicationType,
            AssetType = assetType,
            Ticker = ticker ?? selicCode,
            Description = description ?? issuer.Name,
            IssuerDocument = issuer.Document,
            IssuerName = issuer.Name,
            CountryCode = row.Text("CD_PAIS"),
            MaturityDate = maturity,
            InvestedFundDocument = isFundShare ? issuer.Document : null,
        };
    }

    private static AssetIdentity ChooseIdentity(
        string applicationType, string? assetType, string? selicCode, string? ticker, string? description,
        DateOnly? maturity, Issuer issuer, bool isFundShare)
    {
        if (selicCode is not null)
            return AssetIdentity.ForSelic(selicCode, maturity);

        if (isFundShare && issuer.Document is not null)
            return AssetIdentity.ForFundShare(issuer.Document, null);

        if (ticker is not null)
            return AssetIdentity.ForTicker(ticker);

        if (issuer.DocumentOrName is not null && maturity is not null)
            return AssetIdentity.ForPrivateCredit(issuer.DocumentOrName, assetType, maturity);

        return AssetIdentity.ForOther(applicationType, assetType, description ?? issuer.Name);
    }
}
