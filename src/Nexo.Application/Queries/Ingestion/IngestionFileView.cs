namespace Nexo.Application.Queries.Ingestion;

public sealed record IngestionFileView(
    long Id,
    string SourceKey,
    string Kind,
    string Status,
    long Size,
    string ContentHash,
    int Attempts,
    int RowsRead,
    int RowsAccepted,
    int RowsRejected,
    int PortfoliosChanged,
    string? LastError,
    DateTimeOffset DiscoveredAt,
    DateTimeOffset? ProcessedAt);
