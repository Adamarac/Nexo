namespace Nexo.Application.Queries.Assets;

/// <summary>Um título com todos os vencimentos juntos, ou um ativo sozinho quando não há o que juntar.</summary>
/// <remarks>AssetId é um ativo do grupo, usado para abrir a lista de vencimentos. ISIN e ticker só vêm
/// quando o grupo tem um ativo só, porque cada vencimento tem o seu.</remarks>
public sealed record AssetGroup(
    long AssetId,
    string? Name,
    int Block,
    string? ApplicationType,
    string? AssetType,
    string? IssuerName,
    string? Isin,
    string? Ticker,
    int Assets,
    DateOnly? FirstMaturity,
    DateOnly? LastMaturity);
