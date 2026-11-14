namespace Nexo.Application.Ingestion.Ports;

/// <summary>Grava no banco as carteiras lidas de um zip mensal.</summary>
public interface IPortfolioLoader
{
    Task<IPortfolioLoadSession> BeginAsync(long datasetFileId, CancellationToken cancellationToken);
}
