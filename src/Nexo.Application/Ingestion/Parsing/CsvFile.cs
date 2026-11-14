namespace Nexo.Application.Ingestion.Parsing;

/// <summary>Um CSV de dentro do zip. As linhas são lidas sob demanda, uma de cada vez.</summary>
public sealed record CsvFile(string Name, CsvHeader Header, IEnumerable<CsvRow> Rows);
