namespace Nexo.Domain.Ingestion;

/// <summary>O que o servidor da CVM informa sobre a versão atual de um arquivo.</summary>
public sealed record RemoteVersion(string? ETag, DateTimeOffset? LastModified, long? Size);
