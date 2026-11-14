using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Nexo.Infrastructure.Messaging;

/// <summary>Uma conexão com o RabbitMQ por processo, aberta na primeira vez que alguém precisa.</summary>
internal sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IChannel> CreateChannelAsync(bool withPublisherConfirms, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        // Com confirmação ligada, cada publicação espera o broker dizer "recebi e guardei".
        var channelOptions = withPublisherConfirms
            ? new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true)
            : null;

        return await connection.CreateChannelAsync(channelOptions, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();

        _lock.Dispose();
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory
            {
                Uri = options.Value.Uri,
                // O nome do container aparece no painel do RabbitMQ e diz qual worker é qual.
                ClientProvidedName = $"{options.Value.ClientName}@{Environment.MachineName}",
                AutomaticRecoveryEnabled = true,
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }
}
