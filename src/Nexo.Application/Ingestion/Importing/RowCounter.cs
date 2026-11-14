using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Importing;

internal sealed class RowCounter
{
    public int Accepted { get; private set; }

    public int Rejected { get; private set; }

    public void CountAccepted() => Accepted++;

    public void CountRejected() => Rejected++;

    public IngestionCounts ToCounts(int portfoliosChanged) =>
        new(Accepted + Rejected, Accepted, Rejected, portfoliosChanged);
}
