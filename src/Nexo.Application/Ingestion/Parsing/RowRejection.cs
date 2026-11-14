using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Parsing;

public sealed record RowRejection(int LineNumber, RejectionReason Reason, string Detail, string RawLine);
