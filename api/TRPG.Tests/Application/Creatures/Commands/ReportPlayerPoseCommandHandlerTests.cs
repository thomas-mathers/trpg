using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class ReportPlayerPoseCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private readonly ManualTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ReportPlayerPoseCommandHandler _handler = null!;
    private PlayerPoseStore _poseStore = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<TimeProvider>(_timeProvider)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ReportPlayerPoseCommandHandler>();
        _poseStore = _serviceProvider.GetRequiredService<PlayerPoseStore>();

        _context.States.Add(Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_StoresTheReportedPose_WhenThePlayerIsWalkingInTheLocation()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await _handler.Handle(
            new ReportPlayerPoseCommand
            {
                PlayerId = player.Id,
                LocationId = location.Id,
                X = 12,
                Y = 7,
                Angle = 1.5,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var pose = _poseStore.Find(player.Id);
        Assert.Equal((location.Id, 12d, 7d, 1.5d, true), Describe(pose));
    }

    [Fact]
    public async Task Handle_IgnoresTheReport_WhenItNamesAnotherLocation()
    {
        // Arrange
        var location = await SeedLocation();
        var otherLocation = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await _handler.Handle(
            new ReportPlayerPoseCommand
            {
                PlayerId = player.Id,
                LocationId = otherLocation.Id,
                X = 5,
                Y = 5,
                Angle = 0,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(_poseStore.Find(player.Id));
    }

    [Fact]
    public async Task Handle_IgnoresTheReport_WhenThePlayerIsSitting()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id, CreaturePosture.Sitting);

        // Act
        await _handler.Handle(
            new ReportPlayerPoseCommand
            {
                PlayerId = player.Id,
                LocationId = location.Id,
                X = 5,
                Y = 5,
                Angle = 0,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(_poseStore.Find(player.Id));
    }

    [Fact]
    public async Task Handle_ClampsThePoseToTheLocationBounds()
    {
        // Arrange
        var location = await SeedLocation(width: 20, depth: 10);
        var player = await SeedPlayer(location.Id);

        // Act
        await _handler.Handle(
            new ReportPlayerPoseCommand
            {
                PlayerId = player.Id,
                LocationId = location.Id,
                X = 500,
                Y = -3,
                Angle = 0,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var pose = _poseStore.Find(player.Id);
        Assert.Equal((20d, 0d), (pose!.X, pose.Y));
    }

    [Fact]
    public async Task Handle_KeepsThePreviousPose_WhenTheJumpOutrunsTheWalkingSpeed()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        await Report(player.Id, location.Id, x: 10, y: 10);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Act
        await Report(player.Id, location.Id, x: 60, y: 10);

        // Assert
        var pose = _poseStore.Find(player.Id);
        Assert.Equal(10d, pose!.X);
    }

    [Fact]
    public async Task Handle_AcceptsThePose_WhenTheDistanceMatchesTheElapsedTime()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        await Report(player.Id, location.Id, x: 10, y: 10);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Act
        await Report(player.Id, location.Id, x: 13, y: 10);

        // Assert
        var pose = _poseStore.Find(player.Id);
        Assert.Equal(13d, pose!.X);
    }

    private async Task Report(Guid playerId, Guid locationId, double x, double y) =>
        await _handler.Handle(
            new ReportPlayerPoseCommand
            {
                PlayerId = playerId,
                LocationId = locationId,
                X = x,
                Y = y,
                Angle = 0,
            },
            TestContext.Current.CancellationToken
        );

    private static (Guid, double, double, double, bool) Describe(PlayerPose? pose) =>
        (pose!.LocationId, pose.X, pose.Y, pose.Angle, pose.IsDirty);

    private async Task<Location> SeedLocation(double width = 100, double depth = 100)
    {
        var location = Builders.MakeLocation(WorldId, _stateId, width: width, depth: depth);
        _context.Locations.Add(location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return location;
    }

    private async Task<Creature> SeedPlayer(
        Guid locationId,
        CreaturePosture posture = CreaturePosture.Standing
    )
    {
        var player = Builders.MakeCreature(
            WorldId,
            locationId: locationId,
            posture: posture,
            movementSpeed: 50
        );
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return player;
    }
}
