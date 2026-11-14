namespace Nexo.Application.Queries.Ingestion;

public sealed record RejectionView(long Id, string FileName, int LineNumber, string Reason, string Detail, string RawLine);
