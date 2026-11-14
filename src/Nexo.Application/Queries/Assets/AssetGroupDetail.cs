namespace Nexo.Application.Queries.Assets;

/// <summary>Os ativos de um grupo, do vencimento mais próximo ao mais distante, e quanto os fundos têm de cada
/// um no mês mais recente. Vencimento que nenhum fundo tem no mês não aparece em Holdings.</summary>
public sealed record AssetGroupDetail(
    long AssetId,
    string? Name,
    IReadOnlyList<AssetView> Assets,
    DateOnly? ReferenceDate,
    IReadOnlyList<MaturityHolding> Holdings);

public sealed record MaturityHolding(long AssetId, int Funds, decimal MarketValue);
