namespace Nexo.Application.Ingestion.Parsing;

/// <summary>Uma linha de dados do CSV, já separada em campos.</summary>
public sealed class CsvRow(CsvHeader header, int lineNumber, string raw, string[] fields)
{
    public CsvHeader Header { get; } = header;

    public int LineNumber { get; } = lineNumber;

    public string Raw { get; } = raw;

    public IReadOnlyList<string> Fields { get; } = fields;

    // Campo vazio na CVM quer dizer "não informado", então devolvemos null em vez de texto vazio.
    public string? this[string column]
    {
        get
        {
            var index = Header.IndexOf(column);

            if (index is null || index.Value >= Fields.Count)
                return null;

            var value = Fields[index.Value].Trim();
            return value.Length == 0 ? null : value;
        }
    }
}
