using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Nexo.Application.Ingestion;
using Nexo.Application.Ingestion.Importing;
using Nexo.Application.Ingestion.Ports;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Assets;
using Nexo.Application.Queries.Dashboard;
using Nexo.Application.Queries.Funds;
using Nexo.Application.Queries.Ingestion;
using Nexo.Application.Queries.Portfolios;
using Nexo.Infrastructure.Caching;
using Nexo.Infrastructure.Cvm;
using Nexo.Infrastructure.Messaging;
using Nexo.Infrastructure.Messaging.Outbox;
using Nexo.Infrastructure.Persistence;
using Nexo.Infrastructure.Persistence.Loading;
using Nexo.Infrastructure.Queries;
using Nexo.Infrastructure.Storage;
using Npgsql;

namespace Nexo.Infrastructure;

/// <summary>Registra as peças de infraestrutura. API e Worker escolhem só o que precisam.</summary>
public static class DependencyInjection
{
    private const string UserAgent = "Nexo/1.0";

    public static IServiceCollection AddNexoDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Defina ConnectionStrings:Postgres.");

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddDbContext<NexoDbContext>((provider, options) =>
            PostgresSetup.Configure(options, provider.GetRequiredService<NpgsqlDataSource>()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RedisConnection>();
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.Section));

        return services;
    }

    public static IServiceCollection AddNexoIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.Section));
        services.Configure<CvmOptions>(configuration.GetSection(CvmOptions.Section));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.Section));

        services.AddHttpClient<ICvmSource, CvmHttpSource>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            })
            .AddStandardResilienceHandler(ConfigureCvmResilience);

        services.AddSingleton<IDatasetFileStore, LocalDatasetFileStore>();
        services.AddSingleton<IDatasetArchiveReader, ZipDatasetArchiveReader>();
        services.AddSingleton<IPortfolioLoader, PortfolioLoader>();
        services.AddSingleton<IRegistryLoader, RegistryLoader>();
        services.AddSingleton<ICacheInvalidator, RedisCacheInvalidator>();

        services.AddNexoReprocessing();
        services.AddScoped<IDatasetImporter, PortfolioFileImporter>();
        services.AddScoped<IDatasetImporter, RegistryFileImporter>();
        services.AddScoped<CheckCvmForUpdates>();
        services.AddScoped<ProcessDatasetFile>();

        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddScoped<OutboxRelay>();

        return services;
    }

    public static IServiceCollection AddNexoQueries(this IServiceCollection services)
    {
        services.AddScoped<IFundQueries, FundQueries>();
        services.AddScoped<IPortfolioQueries, PortfolioQueries>();
        services.AddScoped<IAssetQueries, AssetQueries>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IIngestionQueries, IngestionQueries>();
        services.AddSingleton<IQueryCache, RedisQueryCache>();
        services.AddNexoReprocessing();

        return services;
    }

    public static IServiceCollection AddNexoHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgres");
        return services;
    }

    // Reprocessar só grava no banco: o aviso sai pela outbox, que o worker envia.
    public static IServiceCollection AddNexoReprocessing(this IServiceCollection services)
    {
        services.TryAddScoped<IIngestionStore, IngestionStore>();
        services.TryAddScoped<OutboxWriter>();
        services.TryAddScoped<RequestReprocess>();

        return services;
    }

    public static IServiceCollection AddNexoOutboxRelay(this IServiceCollection services) =>
        services.AddHostedService<OutboxRelayService>();

    public static IServiceCollection AddNexoProcessingConsumer(this IServiceCollection services) =>
        services.AddHostedService<DatasetDownloadedConsumer>();

    // Downloads de 15 a 150 MB precisam de mais tempo que o padrão de 10 segundos por tentativa.
    private static void ConfigureCvmResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    }
}
