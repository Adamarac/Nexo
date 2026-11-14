using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.Ingestion;
using Nexo.Application.Ingestion.Messages;
using Nexo.Application.Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Nexo.Infrastructure.Messaging;

/// <summary>Consome os avisos de arquivo baixado, um por vez, e chama o processamento.</summary>
/// <remarks>A confirmação (ack) só sai depois que o resultado foi gravado no banco.</remarks>
internal sealed class DatasetDownloadedConsumer(
    RabbitMqConnection connection,
    RabbitMqPublisher publisher,
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> options,
    ILogger<DatasetDownloadedConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan QueueDepthInterval = TimeSpan.FromSeconds(30);

    private readonly RetryPolicy _retryPolicy = new(options.Value.EffectiveRetryDelays);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await OpenChannelAsync(stoppingToken);

        if (channel is null)
            return;

        await using (channel)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(channel, delivery, stoppingToken);
            await channel.BasicConsumeAsync(RabbitMqTopology.ProcessQueue, autoAck: false, consumer, stoppingToken);

            await ReportQueueDepthUntilStoppedAsync(channel, stoppingToken);
        }
    }

    // O RabbitMQ pode subir depois do worker; tentamos de novo até conseguir.
    private async Task<IChannel?> OpenChannelAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var channel = await connection.CreateChannelAsync(withPublisherConfirms: false, stoppingToken);
                await RabbitMqTopology.DeclareAsync(channel, stoppingToken);
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);
                return channel;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "RabbitMQ indisponível; nova tentativa em {Delay}", ReconnectDelay);
            }

            try
            {
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var envelope = IncomingEnvelope.From(delivery);
        using var activity = NexoTelemetry.ActivitySource.StartActivity("processar arquivo", ActivityKind.Consumer, envelope.TraceParent);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"] = envelope.MessageId,
            ["CorrelationId"] = envelope.CorrelationId,
        });

        NexoTelemetry.MessagesConsumed.Add(1);

        try
        {
            await ProcessAsync(envelope, stoppingToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // O worker está desligando: devolvemos a mensagem para a fila para ninguém perdê-la.
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
        }
    }

    private async Task ProcessAsync(IncomingEnvelope envelope, CancellationToken stoppingToken)
    {
        DatasetDownloaded? message;

        try
        {
            message = JsonSerializer.Deserialize<DatasetDownloaded>(envelope.Body.Span);
        }
        catch (JsonException exception)
        {
            // Mensagem ilegível nunca vai dar certo; vai direto para a DLQ.
            await DeadLetterAsync(envelope, envelope.Attempt, exception.Message, stoppingToken);
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<ProcessDatasetFile>().HandleAsync(message!, stoppingToken);

            if (outcome == ProcessingOutcome.Busy)
                await RetryAsync(envelope, envelope.Attempt, _retryPolicy.ShortestDelay, error: null, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await HandleFailureAsync(envelope, exception, stoppingToken);
        }
    }

    private async Task HandleFailureAsync(IncomingEnvelope envelope, Exception exception, CancellationToken stoppingToken)
    {
        var failures = envelope.Attempt + 1;
        var delay = _retryPolicy.DelayAfterFailure(failures);

        if (delay is null)
        {
            logger.LogError(exception, "Mensagem {MessageId} falhou {Failures} vezes e foi para a DLQ", envelope.MessageId, failures);
            await DeadLetterAsync(envelope, failures, exception.Message, stoppingToken);
            return;
        }

        logger.LogWarning(exception, "Falha ao processar a mensagem {MessageId}; nova tentativa em {Delay}", envelope.MessageId, delay);
        await RetryAsync(envelope, failures, delay.Value, exception.Message, stoppingToken);
    }

    private async Task RetryAsync(IncomingEnvelope envelope, int attempt, TimeSpan delay, string? error, CancellationToken stoppingToken)
    {
        await publisher.PublishAsync(envelope.ToOutgoing(RabbitMqTopology.RetryQueue, attempt) with { Delay = delay, LastError = error }, stoppingToken);
        NexoTelemetry.MessagesRetried.Add(1);
    }

    private async Task DeadLetterAsync(IncomingEnvelope envelope, int attempt, string error, CancellationToken stoppingToken)
    {
        await publisher.PublishAsync(envelope.ToOutgoing(RabbitMqTopology.DeadLetterQueue, attempt) with { LastError = error }, stoppingToken);
        NexoTelemetry.MessagesDeadLettered.Add(1);
    }

    private async Task ReportQueueDepthUntilStoppedAsync(IChannel channel, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                NexoTelemetry.ReportQueueDepth(await channel.MessageCountAsync(RabbitMqTopology.ProcessQueue, stoppingToken));
                await Task.Delay(QueueDepthInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "Não foi possível ler o tamanho da fila");
            }
        }
    }
}
