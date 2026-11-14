namespace Nexo.Application.Ingestion.Parsing;

/// <summary>Cabeçalho de um CSV, que diz em qual posição está cada coluna.</summary>
public sealed class CsvHeader
{
    // Em 2023 algumas colunas tinham outro nome. Aceitamos os dois para ler meses antigos e novos.
    private static readonly Dictionary<string, string[]> OldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CNPJ_FUNDO_CLASSE"] = ["CNPJ_FUNDO"],
        ["TP_FUNDO_CLASSE"] = ["TP_FUNDO"],
        ["CNPJ_FUNDO_CLASSE_COTA"] = ["CNPJ_FUNDO_COTA"],
        ["NM_FUNDO_CLASSE_SUBCLASSE_COTA"] = ["NM_FUNDO_COTA"],
    };

    private readonly Dictionary<string, int> _positions;

    public CsvHeader(IReadOnlyList<string> columns)
    {
        Columns = columns.Select(column => column.Trim()).ToArray();
        _positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < Columns.Count; index++)
            _positions.TryAdd(Columns[index], index);
    }

    public IReadOnlyList<string> Columns { get; }

    public int Count => Columns.Count;

    public int? IndexOf(string column)
    {
        if (_positions.TryGetValue(column, out var index))
            return index;

        foreach (var oldName in OldNames.GetValueOrDefault(column, []))
        {
            if (_positions.TryGetValue(oldName, out index))
                return index;
        }

        return null;
    }

    public IReadOnlyList<string> MissingColumns(IEnumerable<string> required) =>
        required.Where(column => IndexOf(column) is null).ToArray();
}
