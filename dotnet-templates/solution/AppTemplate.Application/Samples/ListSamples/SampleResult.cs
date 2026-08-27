using AppTemplate.Domain.Samples;

namespace AppTemplate.Application.Samples.ListSamples;

public sealed record SampleResult(Guid Id, string Name, string Code, SampleStatus Status);
