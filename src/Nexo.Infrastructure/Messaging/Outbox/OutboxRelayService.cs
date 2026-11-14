using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Messaging.Outbox;

/// <summary>Roda o envio da tabela de saída a cada poucos segundos enquanto o worker estiver no ar.</summary>
internal sealed class OutboxRelayService(IServiceScopeFactory scopes, ILogger<OutboxRelayService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await WaitNextTickAsync(timer, stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OutboxRelay>().PublishPendingAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Falha ao enviar mensagens pendentes da outbox");
            }
        }
    }

    private static async Task<bool> WaitNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
