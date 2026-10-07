using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class PlaceCreaturesAtLocationCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PlaceCreaturesAtLocationCommandHandler _handler = null!;
    private UpdateCreaturesCommandHandler _updateHandler = null!;
    private Location _room = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<PlaceCreaturesAtLocationCommandHandler>();
        _updateHandler = _serviceProvider.GetRequiredService<UpdateCreaturesCommandHandler>();

        var origin = Builders.MakeLocation(worldId: _worldId, width: 20, depth: 20);
        _room = Builders.MakeLocation(worldId: _worldId, width: 10, depth: 10);
        _player = Builders.MakeCreature(
            worldId: _worldId,
            locationId: origin.Id,
            previousLocationId: origin.Id
        );
        var world = Builders.MakeWorld(_worldId);
        world.PlayerId = _player.Id;

        _context.Worlds.Add(world);
        _context.Locations.AddRange(origin, _room);
        _context.LocationConnectors.Add(
            Builders.MakeLocationConnector(
                origin.Id,
                _room.Id,
                worldId: _worldId,
                arrivalX: 2,
                arrivalY: 7,
                arrivalAngle: 1.5
            )
        );
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PutsThePlayerAtTheConnectorArrivalPoint_WhenArrivingFromTheConnectorOrigin()
    {
        // Arrange
        await MoveCreature(_player, _room.Id);

        // Act
        await _handler.Handle(
            new PlaceCreaturesAtLocationCommand
            {
                CreatureIds = [_player.Id],
                LocationId = _room.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var placed = await ReadCreature(_player.Id);
        Assert.Equal((2d, 7d, 1.5), (placed.X, placed.Y, placed.Angle));
    }

    [Fact]
    public async Task Handle_KeepsAnArrivingNpcInsideTheLocation()
    {
        // Arrange
        var npc = Builders.MakeCreature(worldId: _worldId, locationId: _room.Id);
        _context.Creatures.Add(npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new PlaceCreaturesAtLocationCommand { CreatureIds = [npc.Id], LocationId = _room.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var placed = await ReadCreature(npc.Id);
        Assert.InRange(placed.X, 0.3, _room.Width - 0.3);
        Assert.InRange(placed.Y, 0.3, _room.Depth - 0.3);
    }

    [Fact]
    public async Task Handle_LeavesThePose_WhenTheLocationHasNoSize()
    {
        // Arrange
        var sizeless = Builders.MakeLocation(worldId: _worldId);
        var npc = Builders.MakeCreature(worldId: _worldId, locationId: sizeless.Id);
        _context.Locations.Add(sizeless);
        _context.Creatures.Add(npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new PlaceCreaturesAtLocationCommand
            {
                CreatureIds = [npc.Id],
                LocationId = sizeless.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var untouched = await ReadCreature(npc.Id);
        Assert.Equal((0d, 0d, 0d), (untouched.X, untouched.Y, untouched.Angle));
    }

    [Fact]
    public async Task UpdateCreatures_WritesTheArrivalPose_WhenAPlayerChangesLocation()
    {
        // Act
        await _updateHandler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_player.Id], LocationId = _room.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var moved = await ReadCreature(_player.Id);
        Assert.Equal((2d, 7d, 1.5), (moved.X, moved.Y, moved.Angle));
    }

    [Fact]
    public async Task UpdateCreatures_KeepsThePose_WhenTheLocationIsUnchanged()
    {
        // Arrange
        await _updateHandler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_player.Id], LocationId = _room.Id },
            TestContext.Current.CancellationToken
        );
        var poseBefore = await ReadCreature(_player.Id);

        // Act
        await _updateHandler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_player.Id],
                LocationId = _room.Id,
                Name = "Renamed",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var poseAfter = await ReadCreature(_player.Id);
        Assert.Equal(
            (poseBefore.X, poseBefore.Y, poseBefore.Angle),
            (poseAfter.X, poseAfter.Y, poseAfter.Angle)
        );
    }

    [Fact]
    public async Task Handle_RecordsTheConnectorEntryWalk_WhenAnNpcArrivesWhileThePlayerIsPresent()
    {
        // Arrange
        await MoveCreature(_player, _room.Id);
        var npc = await AddArrivingNpc();

        // Act
        await _handler.Handle(
            new PlaceCreaturesAtLocationCommand
            {
                CreatureIds = [npc.Id],
                LocationId = _room.Id,
                ArrivedAt = ArrivedAt,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var placed = await ReadCreature(npc.Id);
        Assert.Equal((2d, 7d, ArrivedAt), (placed.EntryX, placed.EntryY, placed.EnteredAt));
    }

    [Fact]
    public async Task Handle_SettlesTheNpcWithoutAnEntryWalk_WhenThePlayerIsNotPresent()
    {
        // Arrange
        var npc = await AddArrivingNpc();

        // Act
        await _handler.Handle(
            new PlaceCreaturesAtLocationCommand
            {
                CreatureIds = [npc.Id],
                LocationId = _room.Id,
                ArrivedAt = ArrivedAt,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var placed = await ReadCreature(npc.Id);
        Assert.Null(placed.EnteredAt);
    }

    [Fact]
    public async Task Handle_SettlesTheNpcWithoutAnEntryWalk_WhenNoArrivalTimeIsGiven()
    {
        // Arrange
        await MoveCreature(_player, _room.Id);
        var npc = await AddArrivingNpc();

        // Act
        await _handler.Handle(
            new PlaceCreaturesAtLocationCommand { CreatureIds = [npc.Id], LocationId = _room.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var placed = await ReadCreature(npc.Id);
        Assert.Null(placed.EnteredAt);
    }

    [Fact]
    public async Task Handle_ClearsAPreviousEntryWalk_WhenTheNpcIsPlacedAgainWithoutOne()
    {
        // Arrange
        await MoveCreature(_player, _room.Id);
        var npc = await AddArrivingNpc();
        await _handler.Handle(
            new PlaceCreaturesAtLocationCommand
            {
                CreatureIds = [npc.Id],
                LocationId = _room.Id,
                ArrivedAt = ArrivedAt,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new PlaceCreaturesAtLocationCommand { CreatureIds = [npc.Id], LocationId = _room.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var placed = await ReadCreature(npc.Id);
        Assert.Equal((null, null, null), (placed.EntryX, placed.EntryY, placed.EnteredAt));
    }

    [Fact]
    public async Task Handle_ClearsAnExitWalk_WhenTheNpcIsPlaced()
    {
        // Arrange
        var npc = await AddArrivingNpc();
        npc.ExitX = 5;
        npc.ExitY = 5;
        npc.DepartedAt = ArrivedAt;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new PlaceCreaturesAtLocationCommand { CreatureIds = [npc.Id], LocationId = _room.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var placed = await ReadCreature(npc.Id);
        Assert.Equal((null, null, null), (placed.ExitX, placed.ExitY, placed.DepartedAt));
    }

    private static readonly GameInstant ArrivedAt = new(
        new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
    );

    private async Task<Creature> AddArrivingNpc()
    {
        var npc = Builders.MakeCreature(
            worldId: _worldId,
            locationId: _room.Id,
            previousLocationId: _player.PreviousLocationId
        );
        _context.Creatures.Add(npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return npc;
    }

    private async Task MoveCreature(Creature creature, Guid locationId)
    {
        await _context
            .Creatures.Where(candidate => candidate.Id == creature.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.LocationId, locationId),
                TestContext.Current.CancellationToken
            );
    }

    private async Task<Creature> ReadCreature(Guid creatureId)
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == creatureId,
                TestContext.Current.CancellationToken
            );
    }
}
