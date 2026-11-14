namespace Nexo.Application.Ingestion.Ports;

/// <summary>Onde os zips baixados ficam guardados, sem alteração, para poderem ser reprocessados.</summary>
public interface IDatasetFileStore
{
    Task<StoredFile> SaveAsync(string sourceKey, Stream content, CancellationToken cancellationToken);

    Stream OpenRead(string storagePath);
}
