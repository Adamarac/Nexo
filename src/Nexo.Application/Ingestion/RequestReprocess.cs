using Nexo.Application.Ingestion.Messages;
using Nexo.Application.Ingestion.Ports;

namespace Nexo.Application.Ingestion;

/// <summary>Coloca um arquivo já baixado de volta na fila de processamento.</summary>
public sealed class RequestReprocess(IIngestionStore store, TimeProvider clock)
{
    public async Task<ReprocessResult> HandleAsync(long datasetFileId, CancellationToken cancellationToken)
    {
        var file = await store.FindAsync(datasetFileId, cancellationToken);

        if (file is null)
            return ReprocessResult.NotFound;

        // Reprocessar uma versão antiga trocaria os dados novos pelos velhos.
        if (await store.HasNewerVersionAsync(file, cancellationToken))
            return ReprocessResult.NotAllowed;

        if (!file.RequestReprocess(clock.GetUtcNow()))
            return ReprocessResult.NotAllowed;

        var message = DatasetDownloaded.For(file, Guid.NewGuid().ToString("N"), clock.GetUtcNow());
        await store.SaveWithMessageAsync(message, cancellationToken);
        return ReprocessResult.Queued;
    }
}
