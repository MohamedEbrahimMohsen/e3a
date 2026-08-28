using Core.DDD.Models;
using E3A.Application.Catalog.Shared;
using MediatR;

namespace E3A.Application.Catalog.GetCatalog;

public sealed record GetCatalogQuery(string? SearchText, List<string> Tags, CatalogSort Sort = CatalogSort.MostInstalled, int PageNumber = 1, int? PageSize = null) : IRequest<PageData<CatalogEngineerResult>>;
