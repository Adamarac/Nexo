using Nexo.Application.Ingestion.Messages;
using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Ports;

/// <summary>Guarda o estado de cada arquivo baixado e os avisos que precisam sair para o RabbitMQ.</summary>
public interface IIngestionStore
{
    Task<DatasetFile?> FindAsync(long id, CancellationToken cancellationToken);

    Task<DatasetFile?> FindLatestAsync(string sourceKey, CancellationToken cancellationToken);

    Task<DatasetFile?> FindByHashAsync(string sourceKey, string contentHash, CancellationToken cancellationToken);

    // O arquivo e o aviso são gravados juntos: ou os dois entram no banco, ou nenhum entra.
    Task AddDownloadedAsync(DatasetFile file, Func<DatasetFile, DatasetDownloaded> createMessage, CancellationToken cancellationToken);

    // Grava as mudanças pendentes e o aviso na mesma transação.
    Task SaveWithMessageAsync(DatasetDownloaded message, CancellationToken cancellationToken);

    // Devolve false quando outro processo mexeu no mesmo arquivo ao mesmo tempo.
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    // Marca o arquivo como processado e as versões anteriores do mesmo arquivo como substituídas.
    Task CompleteAsync(DatasetFile file, CancellationToken cancellationToken);

    // Há outra versão do mesmo arquivo baixada depois desta?
    Task<bool> HasNewerVersionAsync(DatasetFile file, CancellationToken cancellationToken);
}
