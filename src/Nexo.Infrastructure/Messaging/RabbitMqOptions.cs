namespace Nexo.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string Section = "RabbitMq";

    private static readonly TimeSpan[] DefaultRetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(25)];

    public Uri Uri { get; set; } = new("amqp://localhost:5672");

    public string ClientName { get; set; } = "nexo";

    // Quanto esperar antes de cada nova tentativa. Depois da última, a mensagem vai para a DLQ.
    public TimeSpan[] RetryDelays { get; set; } = [];

    public IReadOnlyList<TimeSpan> EffectiveRetryDelays => RetryDelays.Length > 0 ? RetryDelays : DefaultRetryDelays;
}
