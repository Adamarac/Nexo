using FluentValidation;
using Nexo.Application.Queries.Ingestion;
using Nexo.Domain.Ingestion;

namespace Nexo.Application.Queries.Validation;

public sealed class FileSearchValidator : AbstractValidator<FileSearch>
{
    public FileSearchValidator()
    {
        this.AddPagingRules();
        RuleFor(search => search.Status).MustBeOneOf(Enum.GetNames<DatasetFileStatus>());
    }
}

