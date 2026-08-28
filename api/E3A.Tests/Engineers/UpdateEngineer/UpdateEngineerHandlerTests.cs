using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Engineers.UpdateEngineer;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.UpdateEngineer;

public sealed class UpdateEngineerHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly UpdateEngineerHandler _sut;

    public UpdateEngineerHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new UpdateEngineerHandler(_engineerRepository, _currentUserService, _generator, Options.Create(EngineerFactory.CreateEngineersOptions()));
    }

    [Fact]
    public async Task Handle_ShouldUpdateMetadata_WhenCallerIsOwner()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);

        var result = await _sut.Handle(new UpdateEngineerCommand(engineer.Id, null, "Dive Frontend Engineer", "A frontend engineer.", ["react"]), CancellationToken.None);

        engineer.DisplayName.Should().Be("Dive Frontend Engineer");
        engineer.Description.Should().Be("A frontend engineer.");
        engineer.Tags.Should().Equal("react");
        engineer.Slug.Should().Be(EngineerFactory.DefaultSlug);
        result.DisplayName.Should().Be("Dive Frontend Engineer");
        result.Slug.Should().Be(EngineerFactory.DefaultSlug);
        _engineerRepository.Received(1).Update(engineer);
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new UpdateEngineerCommand(Guid.NewGuid(), null, "Dive Frontend Engineer", null, []), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist()
    {
        _engineerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Engineer?)null);

        var act = async () => await _sut.Handle(new UpdateEngineerCommand(Guid.NewGuid(), null, "Dive Frontend Engineer", null, []), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotFound);
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotOwner()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);

        var act = async () => await _sut.Handle(new UpdateEngineerCommand(engineer.Id, null, "Dive Frontend Engineer", null, []), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotOwned);
        _engineerRepository.DidNotReceive().Update(Arg.Any<Engineer>());
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
