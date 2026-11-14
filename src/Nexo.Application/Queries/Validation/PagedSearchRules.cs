using FluentValidation;

namespace Nexo.Application.Queries.Validation;

internal static class PagedSearchRules
{
    public static void AddPagingRules<T>(this AbstractValidator<T> validator) where T : PagedSearch
    {
        validator.RuleFor(search => search.Page).GreaterThanOrEqualTo(1);
        validator.RuleFor(search => search.PageSize).InclusiveBetween(1, PagedSearch.MaxPageSize);
    }

    public static IRuleBuilderOptions<T, string?> MustBeOneOf<T>(this IRuleBuilder<T, string?> rule, IReadOnlyList<string> options) =>
        rule.Must(value => value is null || options.Contains(value))
            .WithMessage($"Valores aceitos: {string.Join(", ", options)}.");
}
