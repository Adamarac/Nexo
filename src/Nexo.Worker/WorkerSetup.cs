using Nexo.Infrastructure;

namespace Nexo.Worker;

/// <summary>Monta o worker. Fica separado do Program para os testes usarem exatamente a mesma montagem.</summary>
public static class WorkerSetup
{
    public static IHostApplicationBuilder AddNexoWorker(this IHostApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        builder.Services
            .AddNexoDatabase(configuration)
            .AddNexoIngestion(configuration)
            .AddNexoOutboxRelay();

        builder.Services.Configure<WatcherOptions>(configuration.GetSection(WatcherOptions.Section));

        var watcher = configuration.GetSection(WatcherOptions.Section).Get<WatcherOptions>() ?? new WatcherOptions();
        var worker = configuration.GetSection(WorkerOptions.Section).Get<WorkerOptions>() ?? new WorkerOptions();

        if (watcher.Enabled)
            builder.Services.AddHostedService<CvmWatcherService>();

        if (worker.EnableProcessing)
            builder.Services.AddNexoProcessingConsumer();

        return builder;
    }
}
