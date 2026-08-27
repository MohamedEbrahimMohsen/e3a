using E3A.Application.Engineers.Shared;
using E3A.Domain.Engineers;
using MediatR;

namespace E3A.Application.Engineers.ListEngineers;

public sealed class ListEngineersQueryHandler(IEngineerRepository engineerRepository) : IRequestHandler<ListEngineersQuery, List<EngineerResult>>
{
    public async Task<List<EngineerResult>> Handle(ListEngineersQuery request, CancellationToken cancellationToken)
    {
        var engineers = await engineerRepository.FindAsync(x => x.Status == EngineerStatus.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return engineers
            .OrderByDescending(x => x.CreationDate)
            .Select(EngineerResultGenerator.Generate)
            .ToList();
    }
}
