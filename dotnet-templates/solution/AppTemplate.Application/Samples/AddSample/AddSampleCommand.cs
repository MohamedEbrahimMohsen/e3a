using Core.Auditing;
using Core.DDD.Models;
using MediatR;

namespace AppTemplate.Application.Samples.AddSample;

public sealed record AddSampleCommand(LocalizedText Name, string Code) : IRequest<AddSampleResult>, IAuditableCommand
{
    public string AuditAction => "Sample.Create";

    public string AuditResourceType => "Sample";

    public Guid? AuditResourceId => null;
}
