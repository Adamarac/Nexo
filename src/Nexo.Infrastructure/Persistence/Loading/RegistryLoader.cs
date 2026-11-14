using Microsoft.Extensions.Logging;
using Nexo.Application.Ingestion.Ports;
using Npgsql;

namespace Nexo.Infrastructure.Persistence.Loading;

internal sealed class RegistryLoader(NpgsqlDataSource dataSource, ILogger<RegistryLoader> logger) : IRegistryLoader
{
    public async Task<IRegistryLoadSession> BeginAsync(long datasetFileId, CancellationToken cancellationToken) =>
        await RegistryLoadSession.StartAsync(dataSource, datasetFileId, logger, cancellationToken);
}
