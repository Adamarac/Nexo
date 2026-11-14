using FluentValidation;
using Nexo.Application.Queries.Portfolios;

namespace Nexo.Application.Queries.Validation;

public sealed class PositionSearchValidator : AbstractValidator<PositionSearch>
{
    public PositionSearchValidator()
    {
        this.AddPagingRules();
        RuleFor(search => search.Block).InclusiveBetween(0, 8);
        RuleFor(search => search.Search).MaximumLength(100);
        RuleFor(search => search.Sort).MustBeOneOf(PositionSearch.SortOptions);
    }
}

