using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Messages;

/// <summary>Aviso de que um arquivo da CVM foi baixado e está pronto para ser processado.</summary>
/// <remarks>Leva só o necessário para achar o arquivo no banco; o zip nunca viaja na mensagem.</remarks>
public sealed record DatasetDownloaded(
    Guid MessageId,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    long DatasetFileId,
    string SourceKey,
    DatasetKind Kind,
    string ContentHash)
{
    public const string RoutingKey = "dataset.downloaded";

    public static DatasetDownloaded For(DatasetFile file, string correlationId, DateTimeOffset now) =>
        new(Guid.NewGuid(), correlationId, now, file.Id, file.SourceKey, file.Kind, file.ContentHash);
}
