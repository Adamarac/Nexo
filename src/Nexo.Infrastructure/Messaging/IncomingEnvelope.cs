using System.Text;
using RabbitMQ.Client.Events;

namespace Nexo.Infrastructure.Messaging;

/// <summary>Os dados de uma mensagem recebida que importam para processar e para repetir.</summary>
internal sealed record IncomingEnvelope(
    ReadOnlyMemory<byte> Body, string MessageId, string Type, string CorrelationId, string? TraceParent, int Attempt)
{
    public static IncomingEnvelope From(BasicDeliverEventArgs delivery)
    {
        var properties = delivery.BasicProperties;
        var headers = properties.Headers ?? new Dictionary<string, object?>();

        return new IncomingEnvelope(
            delivery.Body.ToArray(),
            properties.MessageId ?? string.Empty,
            properties.Type ?? string.Empty,
            properties.CorrelationId ?? string.Empty,
            ReadText(headers, OutgoingMessage.TraceParentHeader),
            ReadNumber(headers, OutgoingMessage.AttemptHeader));
    }

    // Publicamos direto na fila (exchange vazio) usando o nome da fila como routing key.
    public OutgoingMessage ToOutgoing(string queue, int attempt) =>
        new(string.Empty, queue, Body)
        {
            MessageId = MessageId,
            Type = Type,
            CorrelationId = CorrelationId,
            TraceParent = TraceParent,
            Attempt = attempt,
        };

    // O RabbitMQ devolve textos dos cabeçalhos como bytes.
    private static string? ReadText(IDictionary<string, object?> headers, string key) =>
        headers.TryGetValue(key, out var value) && value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : null;

    private static int ReadNumber(IDictionary<string, object?> headers, string key) =>
        headers.TryGetValue(key, out var value) ? Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) : 0;
}
