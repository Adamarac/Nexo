using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexo.Application.Ingestion.Ports;
using Nexo.Infrastructure.Cvm;
using Nexo.Worker;

namespace Nexo.IntegrationTests.Support;

/// <summary>Worker de verdade, montado como em produção, mas apontando para os containers de teste.</summary>
public static class TestWorker
{
    public static IHost Build(
        InfrastructureFixture infrastructure,
        string connectionString,
        FakeCvmServer cvm,
        bool runBackgroundServices = false,
        Action<IServiceCollection>? configureServices = null,
        string? storageRoot = null)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = connectionString,
            ["ConnectionStrings:Redis"] = infrastructure.RedisConnectionString,
            ["RabbitMq:Uri"] = infrastructure.RabbitMqUri,
            ["RabbitMq:RetryDelays:0"] = "00:00:00.200",
            ["RabbitMq:RetryDelays:1"] = "00:00:00.400",
            ["RabbitMq:RetryDelays:2"] = "00:00:00.600",
            ["Storage:Root"] = storageRoot ?? NewStorageRoot(),
            ["Cvm:BaseUrl"] = FakeCvmServer.BaseUrl.ToString(),
            ["Watcher:Enabled"] = "false",
            ["Worker:EnableProcessing"] = runBackgroundServices.ToString(),
        });

        builder.AddNexoWorker();

        if (!runBackgroundServices)
            builder.Services.RemoveAll<IHostedService>();

        builder.Services.AddHttpClient<ICvmSource, CvmHttpSource>().ConfigurePrimaryHttpMessageHandler(() => cvm);
        configureServices?.Invoke(builder.Services);

        return builder.Build();
    }

    // Workers que dividem a fila precisam dividir também a pasta dos zips, como o volume no Compose.
    public static string NewStorageRoot() => Path.Combine(Path.GetTempPath(), "nexo-tests", Guid.NewGuid().ToString("N"));

    private static void RemoveAll<T>(this IServiceCollection services) where T : class
    {
        foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(T)).ToList())
            services.Remove(descriptor);
    }
}
