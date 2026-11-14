namespace Nexo.Domain.Ingestion;

public enum DatasetFileStatus
{
    Downloaded,
    Processing,
    Processed,
    Failed,
    Superseded,
}
