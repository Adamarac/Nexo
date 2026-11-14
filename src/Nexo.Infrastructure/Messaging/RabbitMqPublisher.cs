using System.Diagnostics;
using Nexo.Application.Observability;
using RabbitMQ.Client;

namespace Nexo.Infrastructure.Messaging;

/// <summary>Publica mensagens e só retorna depois que o RabbitMQ confirma que guardou cada uma.</summary>
internal sealed class RabbitMqPublisher(RabbitMqConnection connection) : IAsyncDisposable
{
    // Um canal do RabbitMQ não aceita publicações em paralelo, então elas passam uma de cada vez.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            await channel.BasicPublishAsync(
                message.Exchange, message.RoutingKey, mandatory: true, message.ToProperties(), message.Body, cancellationToken);

            NexoTelemetry.MessagesPublished.Add(1, new TagList { { "routing_key", message.RoutingKey } });
        }
        catch
        {
            // Depois de um erro o canal pode ter ficado inválido; o próximo envio abre outro.
            await ResetChannelAsync();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ResetChannelAsync();
        _lock.Dispose();
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        _channel = await connection.CreateChannelAsync(withPublisherConfirms: true, cancellationToken);
        await RabbitMqTopology.DeclareAsync(_channel, cancellationToken);
        return _channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is null)
            return;

        await _channel.DisposeAsync();
        _channel = null;
    }
}
