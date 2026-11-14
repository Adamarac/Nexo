using Microsoft.EntityFrameworkCore;
using Nexo.Domain.Assets;
using Nexo.Domain.Funds;
using Nexo.Domain.Ingestion;
using Nexo.Domain.Portfolios;
using Nexo.Infrastructure.Messaging.Outbox;

namespace Nexo.Infrastructure.Persistence;

public sealed class NexoDbContext(DbContextOptions<NexoDbContext> options) : DbContext(options)
{
    public DbSet<Fund> Funds => Set<Fund>();

    public DbSet<FundClass> FundClasses => Set<FundClass>();

    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<DatasetFile> DatasetFiles => Set<DatasetFile>();

    public DbSet<RejectedRow> RejectedRows => Set<RejectedRow>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // pg_trgm permite buscar por pedaços do nome ("itau", "renda fixa") usando índice.
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NexoDbContext).Assembly);
    }
}
