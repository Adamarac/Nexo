namespace Nexo.Domain.Ingestion;

public sealed record IngestionCounts(int RowsRead, int RowsAccepted, int RowsRejected, int PortfoliosChanged);
