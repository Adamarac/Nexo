using FluentValidation;
using Nexo.Application.Queries.Issuers;

namespace Nexo.Application.Queries.Validation;

public sealed class IssuerSearchValidator : AbstractValidator<IssuerSearch>
{
    public IssuerSearchValidator()
    {
        this.AddPagingRules();
        RuleFor(search => search.Search).MaximumLength(100);
        RuleFor(search => search.MinSpellings).GreaterThanOrEqualTo(1);
        RuleFor(search => search.Sort).MustBeOneOf(IssuerSearch.SortOptions);
    }
}
