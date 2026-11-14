using FluentValidation;
using Nexo.Application.Queries.Issuers;

namespace Nexo.Application.Queries.Validation;

public sealed class IssuerFundSearchValidator : AbstractValidator<IssuerFundSearch>
{
    public IssuerFundSearchValidator()
    {
        this.AddPagingRules();
        RuleFor(search => search.Spelling).MaximumLength(300);
    }
}
