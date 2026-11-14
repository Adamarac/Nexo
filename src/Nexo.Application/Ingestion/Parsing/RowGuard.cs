using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Parsing;

/// <summary>Primeira checagem de cada linha, antes de olhar o conteúdo: tamanho certo e sem repetição.</summary>
/// <remarks>Use uma instância por arquivo, porque ela lembra das linhas já vistas.</remarks>
internal sealed class RowGuard(string fileName)
{
    private readonly HashSet<string> _seenRows = new(StringComparer.Ordinal);

    public (RowOrigin Origin, RowRejection? Rejection) Inspect(CsvRow row)
    {
        var origin = new RowOrigin(fileName, row.LineNumber, RowHasher.Hash(row.Raw));

        if (row.Fields.Count != row.Header.Count)
        {
            var detail = $"A linha tem {row.Fields.Count} campos, mas o cabeçalho tem {row.Header.Count}.";
            return (origin, new RowRejection(row.LineNumber, RejectionReason.FieldCountMismatch, detail, row.Raw));
        }

        if (!_seenRows.Add(origin.RowHash))
        {
            var detail = "Linha idêntica a outra já lida neste arquivo.";
            return (origin, new RowRejection(row.LineNumber, RejectionReason.DuplicateRow, detail, row.Raw));
        }

        return (origin, null);
    }
}
