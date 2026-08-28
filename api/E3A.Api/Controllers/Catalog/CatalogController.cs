using E3A.Application.Catalog.GetCatalog;
using E3A.Application.Catalog.GetCatalogEngineer;
using E3A.Application.Catalog.GetCatalogTags;
using E3A.Application.Catalog.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E3A.Api.Controllers.Catalog;

[ApiController]
[Route("api/catalog")]
[AllowAnonymous]
public class CatalogController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetCatalog([FromQuery(Name = "q")] string? searchText, [FromQuery(Name = "tag")] List<string>? tags, [FromQuery] CatalogSort sort = CatalogSort.MostInstalled, [FromQuery(Name = "page")] int pageNumber = 1, [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetCatalogQuery(searchText, tags ?? [], sort, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("tags")]
    public async Task<ActionResult> GetCatalogTags(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCatalogTagsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult> GetCatalogEngineer([FromRoute] string slug, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCatalogEngineerQuery(slug), cancellationToken);
        return Ok(result);
    }
}
