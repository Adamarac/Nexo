using System.Text.Json;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Junta as colunas extras de um bloco num JSON pequeno, guardado junto da posição.</summary>
internal static class DetailsJson
{
    public static string? From(CsvRow row, IReadOnlyList<string> columns)
    {
        var details = new Dictionary<string, string>();

        foreach (var column in columns)
        {
            if (row[column] is { } value)
                details[column] = value;
        }

        return details.Count == 0 ? null : JsonSerializer.Serialize(details);
    }
}
