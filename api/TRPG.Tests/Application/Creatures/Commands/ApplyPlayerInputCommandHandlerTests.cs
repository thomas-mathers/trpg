using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Events;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class ApplyPlayerInputCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly MovementInput Standing = new(0, 0, 0);
    private static readonly MovementInput WalkingNorth = new(1, 0, 0);
    private static readonly double WalkSpeed = InLocationPace.MetersPerRealSecond(50);

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ApplyPlayerInputCommandHandler _handler = null!;
    private PlayerPoseStore _poseStore = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ApplyPlayerInputCommandHandler>();
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
    public async Task Handle_StartsFromTheStoredPosition_WhenThereIsNoPreviousInput()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await Apply(player.Id, location.Id, new MovementInput(1, 0, 1.5), Start, new Point(80, 80));

        // Assert
        var pose = _poseStore.Find(player.Id);
        Assert.Equal((location.Id, 50d, 50d, 1.5d, true), Describe(pose));
    }

    [Fact]
    public async Task Handle_NeverTakesThePositionFromTheClient()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await Apply(player.Id, location.Id, Standing, Start, new Point(80, 80));

        // Assert
        var pose = _poseStore.Find(player.Id);
        Assert.Equal((50d, 50d), (pose!.X, pose.Y));
    }

    [Fact]
    public async Task Handle_AdvancesThePreviousInputUpToTheReceiveTime()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Act
        await Apply(
            player.Id,
            location.Id,
            Standing,
            Start.AddSeconds(0.5),
            new Point(50, 50 - (WalkSpeed * 0.5))
        );

        // Assert
        var pose = _poseStore.Find(player.Id);
        Assert.Equal(50 - (WalkSpeed * 0.5), pose!.Y, 3);
    }

    [Fact]
    public async Task Handle_CapsTheIntegrationGap_WhenInputsAreFarApart()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Act
        await Apply(player.Id, location.Id, Standing, Start.AddSeconds(30), new Point(50, 50));

        // Assert
        var pose = _poseStore.Find(player.Id);
        Assert.Equal(50 - WalkSpeed, pose!.Y, 3);
    }

    [Fact]
    public async Task Handle_StopsAtSolidObstacles()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        _context.Buildings.Add(
            Builders.MakeBuilding(
                exteriorLocationId: location.Id,
                worldId: WorldId,
                x: 50,
                y: 48,
                width: 2,
                depth: 1
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Act
        await Apply(player.Id, location.Id, Standing, Start.AddSeconds(1), new Point(50, 50));

        // Assert
        var pose = _poseStore.Find(player.Id);
        Assert.True(pose!.Y > 48.8);
    }

    [Fact]
    public async Task Handle_EnqueuesACorrection_WhenTheClientDriftsFromTheServerPosition()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await Apply(player.Id, location.Id, Standing, Start, new Point(53, 50));

        // Assert
        var correction = Assert.Single(Events().OfType<PlayerCorrectedEvent>());
        Assert.Equal((player.Id, location.Id, -3d, 0d), Describe(correction));
    }

    [Fact]
    public async Task Handle_EnqueuesNoCorrection_WhenTheClientIsWithinTolerance()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await Apply(player.Id, location.Id, Standing, Start, new Point(50.3, 50));

        // Assert
        Assert.Empty(Events().OfType<PlayerCorrectedEvent>());
    }

    [Fact]
    public async Task Handle_IgnoresTheInput_WhenItNamesAnotherLocation()
    {
        // Arrange
        var location = await SeedLocation();
        var otherLocation = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await Apply(player.Id, otherLocation.Id, Standing, Start, new Point(50, 50));

        // Assert
        Assert.Null(_poseStore.Find(player.Id));
    }

    [Fact]
    public async Task Handle_IgnoresTheInput_WhenThePlayerIsSitting()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id, CreaturePosture.Sitting);

        // Act
        await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Assert
        Assert.Null(_poseStore.Find(player.Id));
    }

    [Fact]
    public async Task Handle_IgnoresTheInput_WhenItIsNotFinite()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await Apply(
            player.Id,
            location.Id,
            new MovementInput(double.NaN, 0, 0),
            Start,
            new Point(50, 50)
        );

        // Assert
        Assert.Null(_poseStore.Find(player.Id));
    }

    private IReadOnlyList<GameClientEvent> Events() =>
        [.. _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents];

    private async Task Apply(
        Guid playerId,
        Guid locationId,
        MovementInput input,
        DateTimeOffset receivedAt,
        Point clientPosition
    ) =>
        await _handler.Handle(
            new ApplyPlayerInputCommand
            {
                WorldId = WorldId,
                PlayerId = playerId,
                LocationId = locationId,
                Input = input,
                ClientPosition = clientPosition,
                ReceivedAt = receivedAt,
            },
            TestContext.Current.CancellationToken
        );

    private static (Guid, Guid, double, double) Describe(PlayerCorrectedEvent correction) =>
        (correction.PlayerId, correction.LocationId, correction.OffsetX, correction.OffsetY);

    private static (Guid, double, double, double, bool) Describe(PlayerPose? pose) =>
        (pose!.LocationId, pose.X, pose.Y, pose.Angle, pose.IsDirty);

    private async Task<Location> SeedLocation()
    {
        var location = Builders.MakeLocation(WorldId, _stateId, width: 100, depth: 100);
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
            movementSpeed: 50,
            x: 50,
            y: 50
        );
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return player;
    }
}
