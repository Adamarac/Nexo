using System.Globalization;
using Nexo.Domain.Common;

namespace Nexo.Domain.Assets;

/// <summary>Chave que diz quando duas linhas da CVM falam do mesmo ativo.</summary>
/// <remarks>Não existe um código único para todo ativo, então cada tipo usa o que a fonte oferece.</remarks>
public sealed record AssetIdentity
{
    private AssetIdentity(string key) => Key = key;

    public string Key { get; }

    public static AssetIdentity ForIsin(string isin) => From("isin", isin);

    public static AssetIdentity ForTicker(string ticker) => From("ticker", ticker);

    public static AssetIdentity ForSelic(string selicCode, DateOnly? maturity) =>
        From("selic", selicCode, Date(maturity));

    public static AssetIdentity ForFundShare(string fundDocument, string? subclassId) =>
        string.IsNullOrWhiteSpace(subclassId) ? From("fund", fundDocument) : From("fund", fundDocument, subclassId);

    public static AssetIdentity ForSwap(string? code, string? description) => From("swap", code, description);

    public static AssetIdentity ForBankDeposit(string? issuer, string? assetType, DateOnly? maturity, string? indexer) =>
        From("bank", issuer, assetType, Date(maturity), indexer);

    public static AssetIdentity ForPrivateCredit(string? issuer, string? assetType, DateOnly? maturity) =>
        From("credit", issuer, assetType, Date(maturity));

    public static AssetIdentity ForForeign(string? country, string? code, string? description) =>
        From("foreign", country, code, description);

    public static AssetIdentity ForOther(string? applicationType, string? assetType, string? description) =>
        From("other", applicationType, assetType, description);

    public override string ToString() => Key;

    private static string? Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static AssetIdentity From(string kind, params string?[] parts) =>
        new($"{kind}:{string.Join(':', parts.Select(TextNormalizer.Normalize))}");
}
