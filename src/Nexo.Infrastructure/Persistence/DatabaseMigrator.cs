using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    // Migrações que reescrevem tabelas grandes (como os 2 milhões de ativos) passam dos 30 s padrão.
    private static readonly TimeSpan MigrationTimeout = TimeSpan.FromMinutes(30);

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();

        db.Database.SetCommandTimeout(MigrationTimeout);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
