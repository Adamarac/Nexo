using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.Ingestion.Importing;
using Nexo.Application.Ingestion.Messages;
using Nexo.Application.Ingestion.Ports;
using Nexo.Application.Observability;
using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion;

/// <summary>Processa um arquivo baixado: lê, valida e grava no banco.</summary>
/// <remarks>Pode receber a mesma mensagem mais de uma vez sem duplicar nada.</remarks>
public sealed class ProcessDatasetFile(
    IIngestionStore store,
    IEnumerable<IDatasetImporter> importers,
    ICacheInvalidator cache,
    TimeProvider clock,
    IOptions<IngestionOptions> options,
    ILogger<ProcessDatasetFile> logger)
{
    public async Task<ProcessingOutcome> HandleAsync(DatasetDownloaded message, CancellationToken cancellationToken)
    {
        var file = await store.FindAsync(message.DatasetFileId, cancellationToken);

        if (file is null)
        {
            logger.LogWarning("Arquivo {DatasetFileId} da mensagem {MessageId} não existe no banco", message.DatasetFileId, message.MessageId);
            return ProcessingOutcome.NotFound;
        }

        var start = file.StartProcessing(clock.GetUtcNow(), options.Value.ProcessingLease);

        if (start == ProcessingStart.AlreadyProcessed)
            return ProcessingOutcome.AlreadyProcessed;

        if (start == ProcessingStart.Busy || !await store.TrySaveChangesAsync(cancellationToken))
            return ProcessingOutcome.Busy;

        await ImportAsync(file, cancellationToken);
        return ProcessingOutcome.Completed;
    }

    private async Task ImportAsync(DatasetFile file, CancellationToken cancellationToken)
    {
        var startedAt = clock.GetTimestamp();
        var kind = new TagList { { "kind", file.Kind.ToString() } };

        try
        {
            var importer = importers.Single(candidate => candidate.Kind == file.Kind);
            var counts = await importer.ImportAsync(file, cancellationToken);

            file.Complete(counts, clock.GetUtcNow());
            await store.CompleteAsync(file, cancellationToken);
            await cache.AdvanceEpochAsync(cancellationToken);

            NexoTelemetry.RowsRead.Add(counts.RowsRead, kind);
            NexoTelemetry.RowsRejected.Add(counts.RowsRejected, kind);
            NexoTelemetry.ProcessingDuration.Record(clock.GetElapsedTime(startedAt).TotalSeconds, kind);

            logger.LogInformation(
                "Arquivo {SourceKey} processado: {RowsRead} linhas lidas, {RowsRejected} rejeitadas, {PortfoliosChanged} registros novos ou alterados",
                file.SourceKey, counts.RowsRead, counts.RowsRejected, counts.PortfoliosChanged);
        }
        catch (OperationCanceledException)
        {
            file.ReleaseLease();
            await store.TrySaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            // Registramos o motivo no arquivo e deixamos o erro subir para a mensagem ir para nova tentativa.
            NexoTelemetry.ProcessingFailures.Add(1, kind);
            file.Fail(exception.Message);
            await store.TrySaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
}
