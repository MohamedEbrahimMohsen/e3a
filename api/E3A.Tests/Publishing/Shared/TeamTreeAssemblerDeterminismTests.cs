using E3A.Application.Publishing.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class TeamTreeAssemblerDeterminismTests
{
    private readonly Guid _ownerUserId = Guid.NewGuid();

    [Fact]
    public void Assemble_ShouldProduceIdenticalZipSha256_WhenCalledTwiceWithTheSameRoster()
    {
        var members = Members();

        var first = DeterministicZipper.Create(Assemble(members));
        var second = DeterministicZipper.Create(Assemble(members));

        first.Sha256.Should().Be(second.Sha256);
    }

    [Fact]
    public void Assemble_ShouldProduceIdenticalZipSha256_WhenMemberInputOrderIsShuffled()
    {
        var members = Members();
        var shuffled = new List<TeamMemberSnapshot> { members[1], members[0] };

        var first = DeterministicZipper.Create(Assemble(members));
        var second = DeterministicZipper.Create(Assemble(shuffled));

        first.Sha256.Should().Be(second.Sha256);
    }

    [Fact]
    public void Assemble_ShouldProduceADifferentZipSha256_WhenAMemberSnapshotGainsAFile()
    {
        var pinnedMembers = Members();
        var largerAlpha = TeamSnapshotFactory.MemberSnapshot("alpha", "agents/reviewer.md", "skills/house-rules/SKILL.md", "skills/new-skill/SKILL.md");

        var fromPinnedSnapshots = DeterministicZipper.Create(Assemble(pinnedMembers));
        var fromLargerSnapshots = DeterministicZipper.Create(Assemble([largerAlpha, pinnedMembers[1]]));

        fromLargerSnapshots.Sha256.Should().NotBe(fromPinnedSnapshots.Sha256);
    }

    private static List<TeamMemberSnapshot> Members()
    {
        return
        [
            TeamSnapshotFactory.MemberSnapshot("alpha", "agents/reviewer.md", "skills/house-rules/SKILL.md"),
            TeamSnapshotFactory.MemberSnapshot("beta", "agents/reviewer.md", "commands/ship.md"),
        ];
    }

    private List<PluginFile> Assemble(List<TeamMemberSnapshot> members)
        => TeamTreeAssembler.Assemble(members, TeamFactory.Draft(_ownerUserId), "1.0.0", "mmohsen", PublishingOptionsFactory.Default());
}
