using FluentValidation;
using Nexo.Application.Queries.Assets;

namespace Nexo.Application.Queries.Validation;

public sealed class ConcentrationSearchValidator : AbstractValidator<ConcentrationSearch>
{
    public ConcentrationSearchValidator() => RuleFor(search => search.Top).InclusiveBetween(1, ConcentrationSearch.MaxTop);
}

