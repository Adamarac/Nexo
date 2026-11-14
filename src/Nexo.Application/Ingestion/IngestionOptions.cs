namespace Nexo.Application.Ingestion;

public sealed class IngestionOptions
{
    public const string Section = "Ingestion";

    // Tempo que um worker fica dono de um arquivo. Se ele cair, outro assume depois desse prazo.
    public TimeSpan ProcessingLease { get; set; } = TimeSpan.FromMinutes(15);
}
