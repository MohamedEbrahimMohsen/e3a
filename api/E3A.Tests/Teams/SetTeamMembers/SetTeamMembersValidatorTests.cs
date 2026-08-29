using E3A.Application.Exceptions;
using E3A.Application.Teams.SetTeamMembers;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Teams.SetTeamMembers;

public sealed class SetTeamMembersValidatorTests
{
    private readonly SetTeamMembersValidator _sut = new(Options.Create(TeamFactory.CreateTeamsOptions()));

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
        => _sut.Validate(new SetTeamMembersCommand(Guid.NewGuid(), [new TeamMemberSelection(Guid.NewGuid(), null)])).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ShouldPass_WhenMembersIsEmpty()
        => _sut.Validate(new SetTeamMembersCommand(Guid.NewGuid(), [])).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ShouldFail_WhenTeamIdIsEmpty()
    {
        var result = _sut.Validate(new SetTeamMembersCommand(Guid.Empty, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamIdRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenMembersExceedTheLimit()
    {
        var members = Enumerable.Range(0, 11).Select(_ => new TeamMemberSelection(Guid.NewGuid(), null)).ToList();

        var result = _sut.Validate(new SetTeamMembersCommand(Guid.NewGuid(), members));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamMemberLimitReached);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTheSameEngineerAppearsTwice()
    {
        var engineerId = Guid.NewGuid();

        var result = _sut.Validate(new SetTeamMembersCommand(Guid.NewGuid(), [new TeamMemberSelection(engineerId, null), new TeamMemberSelection(engineerId, null)]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamMemberDuplicate);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAMemberEngineerIdIsEmpty()
    {
        var result = _sut.Validate(new SetTeamMembersCommand(Guid.NewGuid(), [new TeamMemberSelection(Guid.Empty, null)]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamMemberEngineerIdRequired);
    }
}
