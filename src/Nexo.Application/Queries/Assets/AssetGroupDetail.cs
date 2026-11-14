namespace Nexo.Application.Queries.Assets;

/// <summary>Os ativos de um grupo, do vencimento mais próximo ao mais distante.</summary>
public sealed record AssetGroupDetail(long AssetId, string? Name, IReadOnlyList<AssetView> Assets);
