using Nexo.Application.Ingestion.Ports;
using Npgsql;

namespace Nexo.Infrastructure.Persistence.Loading;

internal sealed class PortfolioLoader(NpgsqlDataSource dataSource) : IPortfolioLoader
{
    public async Task<IPortfolioLoadSession> BeginAsync(long datasetFileId, CancellationToken cancellationToken) =>
        await PortfolioLoadSession.StartAsync(dataSource, datasetFileId, cancellationToken);
}
