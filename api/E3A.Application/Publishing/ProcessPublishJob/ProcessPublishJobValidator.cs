using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Publishing.ProcessPublishJob;

public sealed class ProcessPublishJobValidator : AbstractValidator<ProcessPublishJobCommand>
{
    public ProcessPublishJobValidator()
    {
        RuleFor(x => x.VersionId).ValidateRequired(ErrorCodes.PublishVersionIdRequired);
    }
}
