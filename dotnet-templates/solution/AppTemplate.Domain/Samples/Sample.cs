using Core.DDD.Entities;
using Core.DDD.Models;
using Core.Errors;

namespace AppTemplate.Domain.Samples;

public class Sample : AuditEntity, IAuditEntity
{
    private Sample(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public LocalizedText Name { get; init; } = default!;

    public string Code { get; init; } = default!;

    public SampleStatus Status { get; private set; }

    public static Sample Create(LocalizedText name, string code, Guid? createdBy)
    {
        var sample = new Sample(Guid.NewGuid(), createdBy)
        {
            Name = name,
            Code = code,
            Status = SampleStatus.Draft,
        };

        return sample;
    }

    public void Activate()
    {
        if (Status != SampleStatus.Draft)
        {
            throw new BusinessRuleViolationCoreException(SampleErrorCodes.SampleNotDraft);
        }

        Status = SampleStatus.Active;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        if (Status.IsTerminal())
        {
            throw new BusinessRuleViolationCoreException(SampleErrorCodes.SampleAlreadyArchived);
        }

        Status = SampleStatus.Archived;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
