namespace Nexo.Application.Queries.Dashboard;

public interface IDashboardQueries
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken);
}
