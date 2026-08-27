using MediatR;

namespace AppTemplate.Application.Samples.ListSamples;

public sealed record ListSamplesQuery(bool ActiveOnly) : IRequest<List<SampleResult>>;
