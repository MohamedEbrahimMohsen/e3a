using System.Linq.Expressions;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Reports.SubmitReport;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Domain.Reports;
using E3A.Domain.Teams;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Reports.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Reports.SubmitReport;

public sealed class SubmitReportHandlerTests
{
    private const int MaxReportsPerItem = 3;

    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SubmitReportHandler _sut;

    public SubmitReportHandlerTests()
    {
        _sut = new SubmitReportHandler(_reportRepository, _engineerRepository, _teamRepository, _currentUserService, Options.Create(ReportFactory.CreateReportsOptions(maxReportsPerItem: MaxReportsPerItem)));
    }

    [Fact]
    public async Task Handle_ShouldPersistOpenReport_WhenEngineerExists()
    {
        var engineerId = StubEngineer();
        var before = DateTimeOffset.UtcNow;

        var result = await _sut.Handle(Command(engineerId), CancellationToken.None);

        result.Status.Should().Be(nameof(ReportStatus.Open));
        result.Id.Should().NotBe(Guid.Empty);
        result.CreatedAt.Should().BeOnOrAfter(before);
        await _reportRepository.Received(1).AddAsync(Arg.Is<Report>(x => x.Status == ReportStatus.Open && x.ItemId == engineerId), Arg.Any<CancellationToken>());
        await _reportRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPersistOpenReport_WhenTeamExists()
    {
        var teamId = StubTeam();

        await _sut.Handle(Command(teamId, ItemType.Team), CancellationToken.None);

        await _reportRepository.Received(1).AddAsync(Arg.Is<Report>(x => x.ItemType == ItemType.Team && x.Status == ReportStatus.Open), Arg.Any<CancellationToken>());
        await _teamRepository.Received(1).GetByIdAsync(teamId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>?>(), Arg.Any<bool>());
        await _engineerRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>?>(), Arg.Any<bool>());
        await _reportRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAttributeReportToReporter_WhenCallerIsSignedIn()
    {
        var reporterUserId = Guid.NewGuid();
        _currentUserService.UserId.Returns(reporterUserId);
        var engineerId = StubEngineer();

        await _sut.Handle(Command(engineerId), CancellationToken.None);

        await _reportRepository.Received(1).AddAsync(Arg.Is<Report>(x => x.ReporterUserId == reporterUserId && !x.IsAnonymous), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLeaveReporterUnset_WhenCallerIsAnonymous()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var engineerId = StubEngineer();

        await _sut.Handle(Command(engineerId), CancellationToken.None);

        await _reportRepository.Received(1).AddAsync(Arg.Is<Report>(x => x.ReporterUserId == null && x.IsAnonymous), Arg.Any<CancellationToken>());
        await _reportRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLeaveReporterUnset_WhenCurrentUserIdIsEmpty()
    {
        _currentUserService.UserId.Returns(Guid.Empty);
        var engineerId = StubEngineer();

        await _sut.Handle(Command(engineerId), CancellationToken.None);

        await _reportRepository.Received(1).AddAsync(Arg.Is<Report>(x => x.ReporterUserId == null && x.IsAnonymous), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPersistReport_WhenItemIsOneReportBelowTheCap()
    {
        var engineerId = StubEngineer();
        _reportRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Report, bool>>?>()).Returns(MaxReportsPerItem - 1);

        var result = await _sut.Handle(Command(engineerId), CancellationToken.None);

        result.Status.Should().Be(nameof(ReportStatus.Open));
        await _reportRepository.Received(1).AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
        await _reportRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static SubmitReportCommand Command(Guid itemId, ItemType itemType = ItemType.Engineer)
    {
        return new SubmitReportCommand(itemType, itemId, ReportReason.Malicious, ReportFactory.DefaultDetails);
    }

    private Guid StubEngineer()
    {
        var engineer = EngineerFactory.Published(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>?>(), Arg.Any<bool>()).Returns(engineer);

        return engineer.Id;
    }

    private Guid StubTeam()
    {
        var team = TeamFactory.Published(Guid.NewGuid());
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>?>(), Arg.Any<bool>()).Returns(team);

        return team.Id;
    }
}
