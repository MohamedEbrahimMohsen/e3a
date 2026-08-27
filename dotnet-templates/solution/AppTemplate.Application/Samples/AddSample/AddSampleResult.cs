using Core.Auditing;

namespace AppTemplate.Application.Samples.AddSample;

public sealed record AddSampleResult(Guid SampleId) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => SampleId;
}
