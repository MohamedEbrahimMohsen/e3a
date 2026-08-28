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

public sealed class UpdateEngineerSlugHandlerTests
{
    private const string TypedSlug = "mmohsen";
    private const string SuffixedSlug = "mmohsen-ab12";

    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly UpdateEngineerHandler _sut;

    public UpdateEngineerSlugHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new UpdateEngineerHandler(_engineerRepository, _currentUserService, _generator, Options.Create(EngineerFactory.CreateEngineersOptions()));
    }

    [Fact]
    public async Task Handle_ShouldChangeSlug_WhenEngineerIsDraftAndSlugIsFree()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);
        _engineerRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(new UpdateEngineerCommand(engineer.Id, TypedSlug, EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        engineer.Slug.Should().Be(TypedSlug);
        result.Slug.Should().Be(TypedSlug);
        _engineerRepository.Received(1).Update(engineer);
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNormalizeSlug_WhenSlugHasUppercaseAndWhitespace()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);
        _engineerRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.Handle(new UpdateEngineerCommand(engineer.Id, "  MMohsen  ", EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        engineer.Slug.Should().Be(TypedSlug);
        await _engineerRepository.Received(1).IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldChangeToSuffixedSlug_WhenRequestedSlugIsTaken()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);
        _engineerRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(true);
        _engineerRepository.IsSlugExistsAsync(SuffixedSlug, Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(TypedSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{SuffixedSlug}-");

        await _sut.Handle(new UpdateEngineerCommand(engineer.Id, TypedSlug, EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        engineer.Slug.Should().Be(SuffixedSlug);
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLeaveSlugUnchanged_WhenSlugIsNull()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);

        await _sut.Handle(new UpdateEngineerCommand(engineer.Id, null, EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        engineer.Slug.Should().Be(EngineerFactory.DefaultSlug);
        await _engineerRepository.DidNotReceive().IsSlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLeaveSlugUnchanged_WhenRequestedSlugEqualsCurrentSlug()
    {
        var engineer = EngineerFactory.Published(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);

        await _sut.Handle(new UpdateEngineerCommand(engineer.Id, EngineerFactory.DefaultSlug, EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        engineer.Slug.Should().Be(EngineerFactory.DefaultSlug);
        await _engineerRepository.DidNotReceive().IsSlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenEngineerIsAlreadyPublished()
    {
        var engineer = EngineerFactory.Published(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);

        var act = async () => await _sut.Handle(new UpdateEngineerCommand(engineer.Id, TypedSlug, "Dive Frontend Engineer", null, []), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerSlugFrozen);
        engineer.DisplayName.Should().Be(EngineerFactory.DefaultDisplayName);
        _engineerRepository.DidNotReceive().Update(Arg.Any<Engineer>());
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
