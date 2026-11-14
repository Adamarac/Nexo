using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nexo.IntegrationTests.Support;

/// <summary>Sobe a API de verdade em memória, apontando para o banco e o Redis de teste.</summary>
public sealed class ApiFactory(InfrastructureFixture infrastructure, string connectionString, int permitLimit = 1000) : WebApplicationFactory<Nexo.Api.ApiMarker>
{
    public const string AdminKey = "chave-de-teste";

    // UseSetting entra antes do Program ler a configuração, ao contrário de ConfigureAppConfiguration.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", connectionString);
        builder.UseSetting("ConnectionStrings:Redis", infrastructure.RedisConnectionString);
        builder.UseSetting("Api:AdminApiKey", AdminKey);
        builder.UseSetting("Api:PermitLimit", permitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Api:WindowSeconds", "60");
    }
}
