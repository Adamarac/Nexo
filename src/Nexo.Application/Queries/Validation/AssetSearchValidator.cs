using FluentValidation;
using Nexo.Application.Queries.Assets;

namespace Nexo.Application.Queries.Validation;

public sealed class AssetSearchValidator : AbstractValidator<AssetSearch>
{
    public AssetSearchValidator()
    {
        this.AddPagingRules();
        RuleFor(search => search.Block).InclusiveBetween(0, 8);
        RuleFor(search => search.Search).MaximumLength(100);
    }
}

