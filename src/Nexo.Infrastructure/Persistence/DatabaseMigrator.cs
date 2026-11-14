using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
    }
}
