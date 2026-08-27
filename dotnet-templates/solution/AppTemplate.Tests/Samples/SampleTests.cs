using AppTemplate.Domain.Samples;
using Core.DDD.Models;
using Core.Errors;
using FluentAssertions;
using Xunit;

namespace AppTemplate.Tests.Samples;

public class SampleTests
{
    private static Sample NewSample()
    {
        return Sample.Create(new LocalizedText("عينة", "Sample"), "SMP-1", Guid.NewGuid());
    }

    [Fact]
    public void CreateAlwaysStartsInDraft()
    {
        var sample = NewSample();

        sample.Status.Should().Be(SampleStatus.Draft);
    }

    [Fact]
    public void ActivateWhenDraftBecomesActive()
    {
        var sample = NewSample();

        sample.Activate();

        sample.Status.Should().Be(SampleStatus.Active);
    }

    [Fact]
    public void ActivateWhenAlreadyActiveThrows()
    {
        var sample = NewSample();
        sample.Activate();

        var act = () => sample.Activate();

        act.Should().Throw<BusinessRuleViolationCoreException>();
    }

    [Fact]
    public void ArchiveWhenAlreadyArchivedThrows()
    {
        var sample = NewSample();
        sample.Archive();

        var act = () => sample.Archive();

        act.Should().Throw<BusinessRuleViolationCoreException>();
    }
}
