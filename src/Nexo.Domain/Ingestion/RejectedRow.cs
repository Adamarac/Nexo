namespace Nexo.Domain.Ingestion;

/// <summary>Linha que não entrou no banco, guardada com o motivo para ninguém perder a informação.</summary>
public sealed class RejectedRow
{
    private const int MaxRawLineLength = 2048;

    private RejectedRow()
    {
    }

    public long Id { get; private set; }

    public long DatasetFileId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public int LineNumber { get; private set; }

    public RejectionReason Reason { get; private set; }

    public string Detail { get; private set; } = string.Empty;

    public string RawLine { get; private set; } = string.Empty;

    public static RejectedRow Create(
        long datasetFileId, string fileName, int lineNumber, RejectionReason reason, string detail, string rawLine) => new()
    {
        DatasetFileId = datasetFileId,
        FileName = fileName,
        LineNumber = lineNumber,
        Reason = reason,
        Detail = detail,
        RawLine = rawLine.Length > MaxRawLineLength ? rawLine[..MaxRawLineLength] : rawLine,
    };
}
