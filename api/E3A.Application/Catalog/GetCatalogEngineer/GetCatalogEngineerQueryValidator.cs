using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Catalog.GetCatalogEngineer;

public sealed class GetCatalogEngineerQueryValidator : AbstractValidator<GetCatalogEngineerQuery>
{
    public GetCatalogEngineerQueryValidator()
    {
        RuleFor(x => x.Slug).ValidateRequired(ErrorCodes.CatalogSlugRequired);
    }
}
