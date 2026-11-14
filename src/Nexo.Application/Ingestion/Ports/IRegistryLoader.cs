namespace Nexo.Application.Ingestion.Ports;

/// <summary>Grava no banco o cadastro de fundos e classes.</summary>
public interface IRegistryLoader
{
    Task<IRegistryLoadSession> BeginAsync(long datasetFileId, CancellationToken cancellationToken);
}
