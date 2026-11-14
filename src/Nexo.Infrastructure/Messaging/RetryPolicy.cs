namespace Nexo.Infrastructure.Messaging;

/// <summary>Decide quanto esperar antes de tentar de novo, ou se é hora de desistir.</summary>
internal sealed class RetryPolicy(IReadOnlyList<TimeSpan> delays)
{
    // Devolve null quando as tentativas acabaram e a mensagem deve ir para a DLQ.
    public TimeSpan? DelayAfterFailure(int failures) =>
        failures >= 1 && failures <= delays.Count ? delays[failures - 1] : null;

    public TimeSpan ShortestDelay => delays[0];
}
