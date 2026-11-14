using FluentValidation;
using Nexo.Application.Queries.Funds;

namespace Nexo.Application.Queries.Validation;

public sealed class FundSearchValidator : AbstractValidator<FundSearch>
{
    public FundSearchValidator()
    {
        this.AddPagingRules();
        RuleFor(search => search.Search).MaximumLength(100);
        RuleFor(search => search.Sort).MustBeOneOf(FundSearch.SortOptions);
    }
}

