using System.Globalization;
using RabbitMQ.Client;

namespace Nexo.Infrastructure.Messaging;

/// <summary>Mensagem pronta para sair, com os cabeçalhos que permitem rastrear e repetir.</summary>
internal sealed record OutgoingMessage(string Exchange, string RoutingKey, ReadOnlyMemory<byte> Body)
{
    public const string AttemptHeader = "x-attempt";
    public const string TraceParentHeader = "traceparent";
    public const string LastErrorHeader = "x-last-error";

    public required string MessageId { get; init; }

    public required string Type { get; init; }

    public required string CorrelationId { get; init; }

    public string? TraceParent { get; init; }

    public int Attempt { get; init; }

    public TimeSpan? Delay { get; init; }

    public string? LastError { get; init; }

    public BasicProperties ToProperties()
    {
        var headers = new Dictionary<string, object?> { [AttemptHeader] = Attempt };

        if (TraceParent is not null)
            headers[TraceParentHeader] = TraceParent;

        if (LastError is not null)
            headers[LastErrorHeader] = LastError;

        return new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = MessageId,
            Type = Type,
            CorrelationId = CorrelationId,
            Headers = headers,
            Expiration = Delay is { } delay ? ((long)delay.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) : null,
        };
    }
}
