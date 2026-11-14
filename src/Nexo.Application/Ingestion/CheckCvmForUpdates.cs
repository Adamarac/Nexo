using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Nexo.Application.Ingestion.Messages;
using Nexo.Application.Ingestion.Ports;
using Nexo.Application.Observability;
using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion;

/// <summary>Confere se a CVM publicou arquivos novos ou republicou algum, e baixa o que mudou.</summary>
public sealed class CheckCvmForUpdates(
    ICvmSource cvm,
    IDatasetFileStore fileStore,
    IIngestionStore store,
    TimeProvider clock,
    ILogger<CheckCvmForUpdates> logger)
{
    public async Task<CheckSummary> RunAsync(CancellationToken cancellationToken)
    {
        var sources = await cvm.ListDatasetsAsync(cancellationToken);
        var summary = new CheckSummary();

        foreach (var source in sources)
        {
            try
            {
                var result = await CheckAsync(source, cancellationToken);
                summary.Count(result);
                NexoTelemetry.FilesChecked.Add(1, new TagList { { "result", result.ToString() } });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Um arquivo com problema não pode impedir a conferência dos outros.
                logger.LogError(exception, "Falha ao conferir o arquivo {SourceKey} na CVM", source.SourceKey);
                summary.Count(FileCheckResult.Failed);
                NexoTelemetry.FilesChecked.Add(1, new TagList { { "result", nameof(FileCheckResult.Failed) } });
            }
        }

        logger.LogInformation(
            "Conferência da CVM concluída: {Downloaded} baixados, {Unchanged} sem mudança, {Failed} com falha",
            summary.Downloaded, summary.Unchanged, summary.Failed);
        return summary;
    }

    private async Task<FileCheckResult> CheckAsync(DatasetSource source, CancellationToken cancellationToken)
    {
        var remote = await cvm.GetRemoteVersionAsync(source, cancellationToken);
        var latest = await store.FindLatestAsync(source.SourceKey, cancellationToken);

        if (latest is not null && latest.MatchesRemote(remote))
            return FileCheckResult.Unchanged;

        await using var content = await cvm.OpenReadAsync(source, cancellationToken);
        var stored = await fileStore.SaveAsync(source.SourceKey, content, cancellationToken);

        // Às vezes o servidor troca o ETag sem o conteúdo mudar. Nesse caso só atualizamos o ETag.
        var sameContent = await store.FindByHashAsync(source.SourceKey, stored.ContentHash, cancellationToken);

        if (sameContent is not null)
        {
            sameContent.RefreshRemoteVersion(remote);
            await store.TrySaveChangesAsync(cancellationToken);
            return FileCheckResult.Unchanged;
        }

        var file = DatasetFile.Downloaded(source, remote with { Size = stored.Size }, stored.ContentHash, stored.StoragePath, clock.GetUtcNow());
        var correlationId = Guid.NewGuid().ToString("N");

        await store.AddDownloadedAsync(file, saved => DatasetDownloaded.For(saved, correlationId, clock.GetUtcNow()), cancellationToken);
        NexoTelemetry.FilesDownloaded.Add(1);

        logger.LogInformation(
            "Arquivo {SourceKey} baixado ({Size} bytes, hash {ContentHash}) com correlação {CorrelationId}",
            source.SourceKey, stored.Size, stored.ContentHash, correlationId);
        return FileCheckResult.Downloaded;
    }
}
