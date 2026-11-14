using FluentValidation;
using Nexo.Application.Queries.Assets;

namespace Nexo.Application.Queries.Validation;

public sealed class HolderSearchValidator : AbstractValidator<HolderSearch>
{
    public HolderSearchValidator() => this.AddPagingRules();
}

