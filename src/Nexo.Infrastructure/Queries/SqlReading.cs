using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Nexo.Infrastructure.Persistence;
using Npgsql;

namespace Nexo.Infrastructure.Queries;

/// <summary>Leitura com SQL direto, para consultas que o LINQ não escreve bem.</summary>
internal static class SqlReading
{
    public static async Task<List<T>> ReadAsync<T>(
        this NexoDbContext db, string sql, Action<NpgsqlCommand> bind, Func<DbDataReader, T> read, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        bind(command);

        var rows = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(read(reader));

        return rows;
    }

    public static void Add(this NpgsqlCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    public static string? NullableString(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static DateOnly? NullableDate(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal);
}
