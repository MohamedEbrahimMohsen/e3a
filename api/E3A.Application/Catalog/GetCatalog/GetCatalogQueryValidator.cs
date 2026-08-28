using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace E3A.Application.Catalog.GetCatalog;

public sealed class GetCatalogQueryValidator : AbstractValidator<GetCatalogQuery>
{
    public GetCatalogQueryValidator(IOptions<CatalogOptions> catalogOptions)
    {
        var options = catalogOptions.Value;

        RuleFor(x => x.SearchText).ValidateMaxLength(options.SearchTextMaxLength, ErrorCodes.CatalogSearchTextTooLong);

        RuleFor(x => x.Tags).ValidateListMaxItems(options.MaxTagFilters, ErrorCodes.CatalogTooManyTagFilters);

        RuleForEach(x => x.Tags).ValidateMaxLength(options.TagFilterMaxLength, ErrorCodes.CatalogTagFilterTooLong);

        RuleFor(x => x.Sort)
            .IsInEnum()
            .WithMessage("{PropertyName} must be a known sort option.")
            .WithErrorCode(ErrorCodes.CatalogSortInvalid);

        RuleFor(x => x.PageNumber).ValidatePositive(ErrorCodes.CatalogPageNumberInvalid);

        RuleFor(x => x.PageSize)
            .Must(pageSize => pageSize == null || (pageSize.Value >= 1 && pageSize.Value <= options.MaxPageSize))
            .WithMessage($"{{PropertyName}} must be between 1 and {options.MaxPageSize}.")
            .WithErrorCode(ErrorCodes.CatalogPageSizeInvalid);
    }
}
