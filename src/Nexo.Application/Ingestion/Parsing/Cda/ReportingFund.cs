using Nexo.Domain.Common;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Quem enviou a carteira (fundo ou classe) e de qual data ela é.</summary>
public sealed record ReportingFund(Cnpj Cnpj, string Name, string? FundType, DateOnly ReferenceDate);
