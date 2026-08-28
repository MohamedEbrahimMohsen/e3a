using E3A.Application.Catalog.Shared;
using MediatR;

namespace E3A.Application.Catalog.GetCatalogEngineer;

public sealed record GetCatalogEngineerQuery(string Slug) : IRequest<CatalogEngineerDetailResult>;
