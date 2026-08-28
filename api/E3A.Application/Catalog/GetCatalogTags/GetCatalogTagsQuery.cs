using E3A.Application.Catalog.Shared;
using MediatR;

namespace E3A.Application.Catalog.GetCatalogTags;

public sealed record GetCatalogTagsQuery : IRequest<List<CatalogTagResult>>;
