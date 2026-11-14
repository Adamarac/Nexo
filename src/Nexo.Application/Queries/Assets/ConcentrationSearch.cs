namespace Nexo.Application.Queries.Assets;

// Grouped junta os vencimentos de um mesmo título numa linha só.
public sealed record ConcentrationSearch(DateOnly? ReferenceDate = null, int Top = 20, bool Grouped = false)
{
    public const int MaxTop = 100;
}
