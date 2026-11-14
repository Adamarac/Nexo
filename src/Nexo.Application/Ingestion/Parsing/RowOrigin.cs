namespace Nexo.Application.Ingestion.Parsing;

/// <summary>De onde a linha veio, para dar para rastrear qualquer número até o arquivo original.</summary>
public sealed record RowOrigin(string FileName, int LineNumber, string RowHash);
