using FluentValidation;
using Nexo.Application.Queries.Ingestion;
using Nexo.Domain.Ingestion;

namespace Nexo.Application.Queries.Validation;

public sealed class RejectionSearchValidator : AbstractValidator<RejectionSearch>
{
    public RejectionSearchValidator()
    {
        this.AddPagingRules();
        RuleFor(search => search.Reason).MustBeOneOf(Enum.GetNames<RejectionReason>());
    }
}
