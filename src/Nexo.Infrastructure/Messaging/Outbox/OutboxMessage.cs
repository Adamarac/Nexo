namespace Nexo.Infrastructure.Messaging.Outbox;

/// <summary>Aviso gravado no banco junto com a mudança que ele anuncia, esperando para ir ao RabbitMQ.</summary>
public sealed class OutboxMessage
{
    private const int MaxErrorLength = 1000;

    private OutboxMessage()
    {
    }

    public long Id { get; private set; }

    public Guid MessageId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    // Liga o trace de quem gravou o aviso ao trace de quem vai processar a mensagem.
    public string? TraceParent { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(
        Guid messageId, string type, string routingKey, string payload, string correlationId, string? traceParent, DateTimeOffset now) => new()
    {
        MessageId = messageId,
        Type = type,
        RoutingKey = routingKey,
        Payload = payload,
        CorrelationId = correlationId,
        TraceParent = traceParent,
        CreatedAt = now,
    };

    public void MarkSent(DateTimeOffset now)
    {
        SentAt = now;
        Attempts++;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
    }
}
