namespace Nexo.Application.Queries.Ingestion;

/// <summary>Visão geral da ingestão: quantos arquivos em cada situação e se há avisos parados.</summary>
public sealed record IngestionStatus(
    IReadOnlyDictionary<string, int> FilesByStatus,
    int PendingMessages,
    DateTimeOffset? LastDiscoveredAt,
    DateTimeOffset? LastProcessedAt);
