using Microsoft.EntityFrameworkCore;
using Nexo.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Nexo.IntegrationTests.Support;

/// <summary>Sobe PostgreSQL, RabbitMQ e Redis uma vez para todos os testes de integração.</summary>
/// <remarks>Cada teste ganha um banco próprio copiado de um modelo já migrado, o que leva milissegundos.</remarks>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "nexo_template";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public string RabbitMqUri => _rabbitMq.GetConnectionString();

    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync(), _redis.StartAsync());
        await CreateTemplateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"nexo_{Guid.NewGuid():N}";
        await ExecuteOnServerAsync($"CREATE DATABASE {name} TEMPLATE {TemplateDatabase}");
        return ConnectionStringFor(name);
    }

    private async Task CreateTemplateDatabaseAsync()
    {
        await ExecuteOnServerAsync($"CREATE DATABASE {TemplateDatabase}");

        var options = new DbContextOptionsBuilder<NexoDbContext>()
            .UseNpgsql(ConnectionStringFor(TemplateDatabase), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using (var db = new NexoDbContext(options))
            await db.Database.MigrateAsync();

        // O PostgreSQL só copia um banco modelo se ninguém estiver conectado nele.
        NpgsqlConnection.ClearAllPools();
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    // Prazos maiores que o padrão: os testes rodam junto com outros containers e a máquina pode estar ocupada.
    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = database,
            Timeout = 60,
            CommandTimeout = 120,
        }.ConnectionString;
}
