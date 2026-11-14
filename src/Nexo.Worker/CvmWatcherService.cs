using Microsoft.Extensions.Options;
using Nexo.Application.Ingestion;

namespace Nexo.Worker;

/// <summary>Confere a CVM ao iniciar e depois nos horários configurados.</summary>
internal sealed class CvmWatcherService(
    IServiceScopeFactory scopes, IOptions<WatcherOptions> options, TimeProvider clock, ILogger<CvmWatcherService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = options.Value.ToSchedule();

        if (options.Value.RunOnStartup)
            await CheckOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = schedule.NextRunAfter(clock.GetUtcNow());
            logger.LogInformation("Próxima conferência da CVM em {NextRun}", nextRun);

            if (!await WaitUntilAsync(nextRun, stoppingToken))
                return;

            await CheckOnceAsync(stoppingToken);
        }
    }

    private async Task<bool> WaitUntilAsync(DateTimeOffset moment, CancellationToken stoppingToken)
    {
        try
        {
            var wait = moment - clock.GetUtcNow();
            await Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.Zero, clock, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task CheckOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CheckCvmForUpdates>().RunAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Uma conferência que falha não derruba o worker; a próxima tenta de novo.
            logger.LogError(exception, "Falha ao conferir a CVM");
        }
    }
}
