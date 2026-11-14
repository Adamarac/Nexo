using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexo.Infrastructure.Persistence;

// Usado só pela ferramenta "dotnet ef" para gerar migrations; não abre conexão com o banco.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NexoDbContext>
{
    public NexoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NexoDbContext>();
        PostgresSetup.Configure(options, "Host=localhost;Database=nexo");
        return new NexoDbContext(options.Options);
    }
}
