namespace Nexo.Application.Ingestion;

public enum ProcessingOutcome
{
    Completed,
    AlreadyProcessed,

    // Outro worker está com o arquivo agora; a mensagem volta para a fila e tenta mais tarde.
    Busy,
    NotFound,
}
