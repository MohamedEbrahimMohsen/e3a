using Core.Validation.Extensions;
using FluentValidation;

namespace AppTemplate.Application.Samples.AddSample;

public sealed class AddSampleValidator : AbstractValidator<AddSampleCommand>
{
    public AddSampleValidator()
    {
        RuleFor(x => x.Name.Arabic)
            .ValidateRequired(SampleApplicationErrorCodes.SampleNameArabicRequired);

        RuleFor(x => x.Name.English)
            .ValidateRequired(SampleApplicationErrorCodes.SampleNameEnglishRequired);

        RuleFor(x => x.Code)
            .ValidateRequired(SampleApplicationErrorCodes.SampleCodeRequired);
    }
}
