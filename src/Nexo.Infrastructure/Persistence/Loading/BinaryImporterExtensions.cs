using Npgsql;
using NpgsqlTypes;

namespace Nexo.Infrastructure.Persistence.Loading;

internal static class BinaryImporterExtensions
{
    public static Task WriteNullableAsync<T>(
        this NpgsqlBinaryImporter importer, T? value, NpgsqlDbType type, CancellationToken cancellationToken)
        where T : struct =>
        value.HasValue ? importer.WriteAsync(value.Value, type, cancellationToken) : importer.WriteNullAsync(cancellationToken);

    public static Task WriteNullableAsync(
        this NpgsqlBinaryImporter importer, string? value, NpgsqlDbType type, CancellationToken cancellationToken) =>
        value is null ? importer.WriteNullAsync(cancellationToken) : importer.WriteAsync(value, type, cancellationToken);
}
