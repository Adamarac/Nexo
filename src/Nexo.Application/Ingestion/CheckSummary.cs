namespace Nexo.Application.Ingestion;

public sealed class CheckSummary
{
    public int Downloaded { get; private set; }

    public int Unchanged { get; private set; }

    public int Failed { get; private set; }

    public void Count(FileCheckResult result)
    {
        switch (result)
        {
            case FileCheckResult.Downloaded:
                Downloaded++;
                break;
            case FileCheckResult.Unchanged:
                Unchanged++;
                break;
            case FileCheckResult.Failed:
                Failed++;
                break;
        }
    }
}
