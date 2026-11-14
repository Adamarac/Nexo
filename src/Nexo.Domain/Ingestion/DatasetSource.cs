namespace Nexo.Domain.Ingestion;

/// <summary>Qual arquivo da CVM é, independente da versão.</summary>
public sealed record DatasetSource(string SourceKey, DatasetKind Kind, string Url);
