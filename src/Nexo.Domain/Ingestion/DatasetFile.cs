namespace Nexo.Domain.Ingestion;

/// <summary>Uma versão baixada de um arquivo da CVM e o resultado do seu processamento.</summary>
public sealed class DatasetFile
{
    private const int MaxErrorLength = 2000;

    private DatasetFile()
    {
    }

    public long Id { get; private set; }

    public string SourceKey { get; private set; } = string.Empty;

    public DatasetKind Kind { get; private set; }

    public string Url { get; private set; } = string.Empty;

    public string? ETag { get; private set; }

    public DateTimeOffset? LastModified { get; private set; }

    public long Size { get; private set; }

    public string ContentHash { get; private set; } = string.Empty;

    public string StoragePath { get; private set; } = string.Empty;

    public DatasetFileStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public int RowsRead { get; private set; }

    public int RowsAccepted { get; private set; }

    public int RowsRejected { get; private set; }

    public int PortfoliosChanged { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset DiscoveredAt { get; private set; }

    public DateTimeOffset? ProcessingStartedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public DateTimeOffset? LeaseUntil { get; private set; }

    public static DatasetFile Downloaded(
        DatasetSource source, RemoteVersion remote, string contentHash, string storagePath, DateTimeOffset now) => new()
    {
        SourceKey = source.SourceKey,
        Kind = source.Kind,
        Url = source.Url,
        ETag = remote.ETag,
        LastModified = remote.LastModified,
        Size = remote.Size ?? 0,
        ContentHash = contentHash,
        StoragePath = storagePath,
        Status = DatasetFileStatus.Downloaded,
        DiscoveredAt = now,
    };

    // O servidor da CVM devolve ETag e tamanho num HEAD barato; se os dois batem, não há nada novo.
    public bool MatchesRemote(RemoteVersion remote) =>
        ETag == remote.ETag && (remote.Size is null || Size == remote.Size);

    public void RefreshRemoteVersion(RemoteVersion remote)
    {
        ETag = remote.ETag;
        LastModified = remote.LastModified;
    }

    // O "lease" é um prazo de posse: se o worker cair no meio, outro pode assumir quando ele vencer.
    public ProcessingStart StartProcessing(DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (Status is DatasetFileStatus.Processed or DatasetFileStatus.Superseded)
            return ProcessingStart.AlreadyProcessed;

        if (Status == DatasetFileStatus.Processing && LeaseUntil > now)
            return ProcessingStart.Busy;

        Status = DatasetFileStatus.Processing;
        Attempts++;
        LastError = null;
        ProcessingStartedAt = now;
        LeaseUntil = now + leaseDuration;
        return ProcessingStart.Started;
    }

    public void Complete(IngestionCounts counts, DateTimeOffset now)
    {
        EnsureStatus(DatasetFileStatus.Processing);

        if (counts.RowsRead != counts.RowsAccepted + counts.RowsRejected)
            throw new InvalidOperationException("Toda linha lida precisa ser aceita ou rejeitada.");

        Status = DatasetFileStatus.Processed;
        RowsRead = counts.RowsRead;
        RowsAccepted = counts.RowsAccepted;
        RowsRejected = counts.RowsRejected;
        PortfoliosChanged = counts.PortfoliosChanged;
        ProcessedAt = now;
        LeaseUntil = null;
    }

    public void Fail(string error)
    {
        Status = DatasetFileStatus.Failed;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        LeaseUntil = null;
    }

    // Usado quando o worker é desligado no meio: devolve o arquivo para a fila sem esperar o prazo vencer.
    public void ReleaseLease()
    {
        if (Status != DatasetFileStatus.Processing)
            return;

        Status = DatasetFileStatus.Downloaded;
        LeaseUntil = null;
    }

    // Vale também para a versão que ainda espera na fila: ela não pode rodar depois da nova e desfazê-la.
    public void Supersede()
    {
        if (Status is DatasetFileStatus.Processed or DatasetFileStatus.Downloaded or DatasetFileStatus.Failed)
            Status = DatasetFileStatus.Superseded;
    }

    public bool RequestReprocess(DateTimeOffset now)
    {
        var isBeingProcessed = Status == DatasetFileStatus.Processing && LeaseUntil > now;

        if (isBeingProcessed || Status == DatasetFileStatus.Superseded)
            return false;

        Status = DatasetFileStatus.Downloaded;
        LeaseUntil = null;
        return true;
    }

    private void EnsureStatus(DatasetFileStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Esperado status {expected}, mas o arquivo está em {Status}.");
    }
}
