namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Patrimônio líquido (PL) de um fundo na data de referência.</summary>
public sealed record NetWorthRecord(ReportingFund Fund, decimal? NetWorth, RowOrigin Origin);
