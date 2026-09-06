using System.Linq.Expressions;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Reports.SubmitReport;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Domain.Reports;
using E3A.Domain.Teams;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Reports.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Reports.SubmitReport;

public sealed class SubmitReportHandlerGuardTests
{
    private const int MaxReportsPerItem = 3;

    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SubmitReportHandler _sut;

    public SubmitReportHandlerGuardTests()
    {
        _sut = new SubmitReportHandler(_reportRepository, _engineerRepository, _teamRepository, _currentUserService, Options.Create(ReportFactory.CreateReportsOptions(maxReportsPerItem: MaxReportsPerItem)));
    }

    [Fact]
    public async Task Handle_ShouldThrowBadRequest_WhenEngineerDoesNotExist()
    {
        _engineerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>?>(), Arg.Any<bool>()).Returns((Engineer?)null);

        var act = async () => await _sut.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.ReportItemNotFound);
        await _reportRepository.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
        await _reportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBadRequest_WhenTeamDoesNotExist()
    {
        _teamRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>?>(), Arg.Any<bool>()).Returns((Team?)null);

        var act = async () => await _sut.Handle(Command(Guid.NewGuid(), ItemType.Team), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.ReportItemNotFound);
        await _reportRepository.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
        await _reportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowRateLimitExceeded_WhenItemReachedTheReportCap()
    {
        var engineer = EngineerFactory.Published(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>?>(), Arg.Any<bool>()).Returns(engineer);
        _reportRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Report, bool>>?>()).Returns(MaxReportsPerItem);

        var act = async () => await _sut.Handle(Command(engineer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExceededCoreException>()
            .Where(x => x.ErrorCode == ErrorCodes.ReportLimitReached && x.Context != null && (int)x.Context["limit"] == MaxReportsPerItem);
        await _reportRepository.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
        await _reportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static SubmitReportCommand Command(Guid itemId, ItemType itemType = ItemType.Engineer)
    {
        return new SubmitReportCommand(itemType, itemId, ReportReason.Malicious, ReportFactory.DefaultDetails);
    }
}
