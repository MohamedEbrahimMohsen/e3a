using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Publishing.GetPublishStatus;

public sealed class GetPublishStatusQueryValidator : AbstractValidator<GetPublishStatusQuery>
{
    public GetPublishStatusQueryValidator()
    {
        RuleFor(x => x.VersionId).ValidateRequired(ErrorCodes.PublishVersionIdRequired);
    }
}
