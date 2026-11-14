using Nexo.Application.Ingestion.Parsing;

namespace Nexo.Application.Ingestion.Importing;

internal static class RequiredColumns
{
    // Sem uma coluna obrigatória nenhuma linha do arquivo serve, então o arquivo inteiro falha com motivo claro.
    public static void EnsurePresent(CsvFile csv, IEnumerable<string> required)
    {
        var missing = csv.Header.MissingColumns(required);

        if (missing.Count > 0)
            throw new InvalidDataException($"O arquivo {csv.Name} não tem as colunas obrigatórias: {string.Join(", ", missing)}.");
    }
}
