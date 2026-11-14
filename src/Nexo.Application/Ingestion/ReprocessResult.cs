namespace Nexo.Application.Ingestion;

public enum ReprocessResult
{
    Queued,
    NotFound,

    // O arquivo está sendo processado agora ou já foi substituído por uma versão mais nova.
    NotAllowed,
}
