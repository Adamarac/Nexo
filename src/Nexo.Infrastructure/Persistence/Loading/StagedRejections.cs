using Nexo.Application.Ingestion.Parsing;
using Npgsql;
using NpgsqlTypes;

namespace Nexo.Infrastructure.Persistence.Loading;

/// <summary>Junta as linhas rejeitadas e manda para uma tabela temporária em lotes.</summary>
/// <remarks>Guardamos o texto das primeiras 10 mil por arquivo; as demais só entram na contagem.</remarks>
internal sealed class StagedRejections(NpgsqlConnection connection)
{
    public const string CreateTableSql = """
        DROP TABLE IF EXISTS stage_rejections;
        CREATE TEMP TABLE stage_rejections (
            file_name text NOT NULL,
            line_number integer NOT NULL,
            reason text NOT NULL,
            detail text NOT NULL,
            raw_line text NOT NULL
        );
        """;

    public const string MoveToFinalTableSql = """
        DELETE FROM rejected_rows WHERE dataset_file_id = @datasetFileId;
        INSERT INTO rejected_rows (dataset_file_id, file_name, line_number, reason, detail, raw_line)
        SELECT @datasetFileId, file_name, line_number, reason, detail, raw_line FROM stage_rejections;
        """;

    private const int BatchSize = 1_000;
    private const int MaxStoredRejections = 10_000;
    private const int MaxRawLineLength = 2_048;

    private readonly List<(string FileName, RowRejection Rejection)> _buffer = [];
    private int _stored;

    public async Task AddAsync(string fileName, RowRejection rejection, CancellationToken cancellationToken)
    {
        if (_stored + _buffer.Count >= MaxStoredRejections)
            return;

        _buffer.Add((fileName, rejection));

        if (_buffer.Count >= BatchSize)
            await FlushAsync(cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_buffer.Count == 0)
            return;

        await using var importer = await connection.BeginBinaryImportAsync(
            "COPY stage_rejections (file_name, line_number, reason, detail, raw_line) FROM STDIN (FORMAT BINARY)", cancellationToken);

        foreach (var (fileName, rejection) in _buffer)
        {
            await importer.StartRowAsync(cancellationToken);
            await importer.WriteAsync(fileName, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteAsync(rejection.LineNumber, NpgsqlDbType.Integer, cancellationToken);
            await importer.WriteAsync(rejection.Reason.ToString(), NpgsqlDbType.Text, cancellationToken);
            await importer.WriteAsync(rejection.Detail, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteAsync(Truncate(rejection.RawLine), NpgsqlDbType.Text, cancellationToken);
        }

        await importer.CompleteAsync(cancellationToken);
        _stored += _buffer.Count;
        _buffer.Clear();
    }

    private static string Truncate(string line) => line.Length > MaxRawLineLength ? line[..MaxRawLineLength] : line;
}
