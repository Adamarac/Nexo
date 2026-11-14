using Npgsql;

namespace Nexo.Infrastructure.Persistence.Loading;

internal static class NpgsqlConnectionExtensions
{
    // Comandos de carga podem demorar em meses grandes; o padrão de 30 s seria curto demais.
    private const int LoadCommandTimeoutSeconds = 600;

    public static Task ExecuteAsync(this NpgsqlConnection connection, string sql, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, sql, datasetFileId: null, cancellationToken);

    public static async Task ExecuteAsync(
        this NpgsqlConnection connection, string sql, long? datasetFileId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, sql, datasetFileId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Devolve quantas linhas o comando inseriu ou alterou.
    public static async Task<int> ExecuteCountAsync(this NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, sql, datasetFileId: null);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<T> ScalarAsync<T>(this NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, sql, datasetFileId: null);
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql, long? datasetFileId)
    {
        var command = new NpgsqlCommand(sql, connection) { CommandTimeout = LoadCommandTimeoutSeconds };

        if (datasetFileId is not null)
            command.Parameters.AddWithValue("datasetFileId", datasetFileId.Value);

        return command;
    }
}
