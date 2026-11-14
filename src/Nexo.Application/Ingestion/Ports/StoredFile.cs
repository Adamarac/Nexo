namespace Nexo.Application.Ingestion.Ports;

public sealed record StoredFile(string StoragePath, string ContentHash, long Size);
