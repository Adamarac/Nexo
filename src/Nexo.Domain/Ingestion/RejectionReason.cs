namespace Nexo.Domain.Ingestion;

public enum RejectionReason
{
    FieldCountMismatch,
    InvalidCnpj,
    InvalidDate,
    InvalidNumber,
    MissingRequiredValue,
    ReferenceDateMismatch,
    DuplicateRow,
    InconsistentNetWorth,
}
