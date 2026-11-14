namespace Nexo.Application.Queries.Assets;

/// <summary>Ativos com mais dinheiro investido numa data, somando todos os fundos.</summary>
public sealed record ConcentrationReport(DateOnly? ReferenceDate, IReadOnlyList<ConcentrationItem> Items);
