namespace Nexo.Application.Queries.Assets;

public sealed record ConcentrationSearch(DateOnly? ReferenceDate = null, int Top = 20)
{
    public const int MaxTop = 100;
}
