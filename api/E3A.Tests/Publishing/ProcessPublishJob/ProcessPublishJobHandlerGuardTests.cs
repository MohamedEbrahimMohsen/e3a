using Core.Azure.Clients;
using Core.Errors;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.ProcessPublishJob;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.ProcessPublishJob;

public sealed class ProcessPublishJobHandlerGuardTests
{
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly Guid _engineerId = Guid.NewGuid();
    private readonly ProcessPublishJobHandler _sut;

    public ProcessPublishJobHandlerGuardTests()
    {
        _sut = new ProcessPublishJobHandler(_itemVersionRepository, _engineerRepository, Substitute.For<ITeamRepository>(), _userRepository, _storageBlobClient, Options.Create(new AzureOptions()), Options.Create(PublishingOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenVersionDoesNotExist()
    {
        _itemVersionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ItemVersion?)null);

        var act = async () => await _sut.Handle(new ProcessPublishJobCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.PublishVersionNotFound);
        await _itemVersionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _storageBlobClient.DidNotReceive().ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ItemVersionStatus.Published)]
    [InlineData(ItemVersionStatus.Failed)]
    public async Task Handle_ShouldDoNothing_WhenVersionIsTerminal(ItemVersionStatus status)
    {
        var version = status == ItemVersionStatus.Published
            ? ItemVersionFactory.Published(_engineerId)
            : ItemVersionFactory.Failed(_engineerId, ErrorCodes.PluginTooLarge);
        _itemVersionRepository.GetByIdAsync(version.Id, Arg.Any<CancellationToken>()).Returns(version);

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        await _itemVersionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _storageBlobClient.DidNotReceive().ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
