using Nexo.Application.Queries.Ingestion;

namespace Nexo.Application.Queries.Dashboard;

public sealed record DashboardSummary(
    int Funds,
    int FundsWithPortfolio,
    int Assets,
    int Portfolios,
    DateOnly? LatestReferenceDate,
    DateTimeOffset? LastProcessedAt,
    IReadOnlyList<CountByLabel> FundsByType,
    IReadOnlyList<IngestionFileView> RecentFiles);
