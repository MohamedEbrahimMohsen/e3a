using AppTemplate.Domain.Samples;
using Core.Localization;
using MediatR;

namespace AppTemplate.Application.Samples.ListSamples;

public sealed class ListSamplesQueryHandler(ISampleRepository sampleRepository) : IRequestHandler<ListSamplesQuery, List<SampleResult>>
{
    public async Task<List<SampleResult>> Handle(ListSamplesQuery request, CancellationToken cancellationToken)
    {
        var samples = await sampleRepository.FindAsync(x => !request.ActiveOnly || x.Status == SampleStatus.Active, cancellationToken: cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var results = samples?
            .Select(x => new SampleResult(x.Id, x.Name.Localized(), x.Code, x.Status))
            .ToList() ?? [];

        return results;
    }
}
