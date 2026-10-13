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

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ApplyPlayerInputCommandHandler>();

        _context.States.Add(Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_StartsFromTheStoredPosition_WhenThereIsNoPreviousPose()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            new MovementInput(1, 0, 1.5),
            Start,
            new Point(80, 80)
        );

        // Assert
        Assert.Equal((location.Id, 50d, 50d, 1.5d), Describe(pose));
    }

    [Fact]
    public async Task Handle_NeverTakesThePositionFromTheClient()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        var pose = await Apply(player.Id, location.Id, Standing, Start, new Point(80, 80));

        // Assert
        Assert.Equal((50d, 50d), (pose!.Position.X, pose.Position.Y));
    }

    [Fact]
    public async Task Handle_AdvancesThePreviousInputUpToTheReceiveTime()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        var previous = await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            Standing,
            Start.AddSeconds(0.5),
            new Point(50, 50 - (WalkSpeed * 0.5)),
            previous
        );

        // Assert
        Assert.Equal(50 - (WalkSpeed * 0.5), pose!.Position.Y, 3);
    }

    [Fact]
    public async Task Handle_AdvancesInWholeSteps_WhenTheGapIsNotAMultipleOfTheStep()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        var previous = await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            WalkingNorth,
            Start.AddSeconds(0.026),
            new Point(50, 50),
            previous
        );

        // Assert
        Assert.Equal(50 - (WalkSpeed * PlayerMovementIntegrator.StepSeconds), pose!.Position.Y, 6);
    }

    [Fact]
    public async Task Handle_CarriesTheLeftoverTimeIntoTheNextGap()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        var first = await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));
        var second = await Apply(
            player.Id,
            location.Id,
            WalkingNorth,
            Start.AddSeconds(0.026),
            new Point(50, 50),
            first
        );

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            Standing,
            Start.AddSeconds(0.05),
            new Point(50, 50),
            second
        );

        // Assert
        Assert.Equal(
            50 - (WalkSpeed * PlayerMovementIntegrator.StepSeconds * 3),
            pose!.Position.Y,
            6
        );
    }

    [Fact]
    public async Task Handle_TurnsTheHeadingGraduallyAcrossTheGap_WhenTheHeadingChanged()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        var previous = await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            new MovementInput(1, 0, Math.PI / 2),
            Start.AddSeconds(0.5),
            new Point(50, 50),
            previous
        );

        // Assert
        var swept = WalkSpeed * 0.5 * 2 / Math.PI;
        Assert.InRange(pose!.Position.X, 50 + swept - 0.01, 50 + swept + 0.01);
        Assert.InRange(pose.Position.Y, 50 - swept - 0.01, 50 - swept + 0.01);
    }

    [Fact]
    public async Task Handle_TurnsTheShortWayRound_WhenTheHeadingCrossesTheAntiNorthSeam()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        var previous = await Apply(
            player.Id,
            location.Id,
            new MovementInput(1, 0, Math.PI - 0.1),
            Start,
            new Point(50, 50)
        );

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            new MovementInput(1, 0, -Math.PI + 0.1),
            Start.AddSeconds(0.5),
            new Point(50, 50),
            previous
        );

        // Assert
        Assert.True(pose!.Position.Y > 50);
    }

    [Fact]
    public async Task Handle_CapsTheIntegrationGap_WhenInputsAreFarApart()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        var previous = await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            Standing,
            Start.AddSeconds(30),
            new Point(50, 50),
            previous
        );

        // Assert
        Assert.Equal(50 - WalkSpeed, pose!.Position.Y, 3);
    }

    [Fact]
    public async Task Handle_StartsFromTheStoredPosition_WhenThePreviousPoseIsInAnotherLocation()
    {
        // Arrange
        var location = await SeedLocation();
        var otherLocation = await SeedLocation();
        var player = await SeedPlayer(location.Id);
        var previous = new PlayerPose(
            otherLocation.Id,
            new Point(10, 10),
            Start,
            WalkingNorth,
            default
        );

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            Standing,
            Start.AddSeconds(0.5),
            new Point(50, 50),
            previous
        );

        // Assert
        Assert.Equal((50d, 50d), (pose!.Position.X, pose.Position.Y));
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
        var previous = await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            Standing,
            Start.AddSeconds(1),
            new Point(50, 50),
            previous
        );

        // Assert
        Assert.True(pose!.Position.Y > 48.8);
    }

    [Fact]
    public async Task Handle_EnqueuesACorrection_WhenTheClientDriftsFromTheServerPosition()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await Apply(player.Id, location.Id, Standing, Start, new Point(55, 50));

        // Assert
        var correction = Assert.Single(Events().OfType<PlayerCorrectedEvent>());
        Assert.Equal((player.Id, location.Id, 50d, 50d), Describe(correction));
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
    public async Task Handle_EnqueuesNoCorrection_WhenTheClientIsOneMeterOff()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        await Apply(player.Id, location.Id, Standing, Start, new Point(51, 50));

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
        var pose = await Apply(player.Id, otherLocation.Id, Standing, Start, new Point(50, 50));

        // Assert
        Assert.Null(pose);
    }

    [Fact]
    public async Task Handle_IgnoresTheInput_WhenThePlayerIsSitting()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id, CreaturePosture.Sitting);

        // Act
        var pose = await Apply(player.Id, location.Id, WalkingNorth, Start, new Point(50, 50));

        // Assert
        Assert.Null(pose);
    }

    [Fact]
    public async Task Handle_IgnoresTheInput_WhenItIsNotFinite()
    {
        // Arrange
        var location = await SeedLocation();
        var player = await SeedPlayer(location.Id);

        // Act
        var pose = await Apply(
            player.Id,
            location.Id,
            new MovementInput(double.NaN, 0, 0),
            Start,
            new Point(50, 50)
        );

        // Assert
        Assert.Null(pose);
    }

    private IReadOnlyList<GameClientEvent> Events() =>
        [.. _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents];

    private async Task<PlayerPose?> Apply(
        Guid playerId,
        Guid locationId,
        MovementInput input,
        DateTimeOffset receivedAt,
        Point clientPosition,
        PlayerPose? previous = null
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
                Previous = previous,
            },
            TestContext.Current.CancellationToken
        );

    private static (Guid, Guid, double, double) Describe(PlayerCorrectedEvent correction) =>
        (correction.PlayerId, correction.LocationId, correction.X, correction.Y);

    private static (Guid, double, double, double) Describe(PlayerPose? pose) =>
        (pose!.LocationId, pose.Position.X, pose.Position.Y, pose.Input.Heading);

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
