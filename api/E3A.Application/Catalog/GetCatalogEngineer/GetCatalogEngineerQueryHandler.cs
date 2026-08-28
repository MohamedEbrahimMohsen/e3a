using Core.Errors;
using E3A.Application.Catalog.Shared;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using MediatR;

namespace E3A.Application.Catalog.GetCatalogEngineer;

public sealed class GetCatalogEngineerQueryHandler(IEngineerRepository engineerRepository) : IRequestHandler<GetCatalogEngineerQuery, CatalogEngineerDetailResult>
{
    public async Task<CatalogEngineerDetailResult> Handle(GetCatalogEngineerQuery request, CancellationToken cancellationToken)
    {
        var engineer = await engineerRepository.FirstOrDefaultAsync(x => x.Slug == request.Slug && x.Status == EngineerStatus.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        if (engineer == null)
        {
            throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
        }

        return CatalogEngineerResultGenerator.GenerateDetail(engineer);
    }
}
