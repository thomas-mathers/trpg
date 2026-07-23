using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetAllNearbyCreaturesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetAllNearbyCreaturesQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllNearbyCreaturesQueryHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCreaturesInRoom_WhenLocationHasRoomId()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var inRoom = Builders.MakeCreature(WorldId, stateId: stateId, roomId: roomId);
        var outdoors = Builders.MakeCreature(WorldId, stateId: stateId);
        _context.Creatures.AddRange(inRoom, outdoors);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(WorldId, roomId, stateId, null);

        // Act
        var result = await _handler.Handle(
            new GetAllNearbyCreaturesQuery { Location = location },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, x => x.Id == inRoom.Id);
        Assert.DoesNotContain(result, x => x.Id == outdoors.Id);
    }

    [Fact]
    public async Task Handle_ReturnsCreaturesOutdoors_WhenLocationHasNoRoomId()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var outdoors = Builders.MakeCreature(WorldId, stateId: stateId, districtId: districtId);
        var indoors = Builders.MakeCreature(
            WorldId,
            stateId: stateId,
            districtId: districtId,
            roomId: Guid.NewGuid()
        );
        _context.Creatures.AddRange(outdoors, indoors);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(WorldId, null, stateId, districtId);

        // Act
        var result = await _handler.Handle(
            new GetAllNearbyCreaturesQuery { Location = location },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, x => x.Id == outdoors.Id);
        Assert.DoesNotContain(result, x => x.Id == indoors.Id);
    }

    [Fact]
    public async Task Handle_ExcludesGivenCreature()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var player = Builders.MakeCreature(WorldId, stateId: stateId, roomId: roomId);
        var other = Builders.MakeCreature(WorldId, stateId: stateId, roomId: roomId);
        _context.Creatures.AddRange(player, other);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(WorldId, roomId, stateId, null);

        // Act
        var result = await _handler.Handle(
            new GetAllNearbyCreaturesQuery { Location = location, ExcludingCreatureId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(result, x => x.Id == player.Id);
        Assert.Contains(result, x => x.Id == other.Id);
    }

    [Fact]
    public async Task Handle_IncludesDeadCreatures_ByDefault()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var corpse = Builders.MakeCreature(
            WorldId,
            stateId: stateId,
            roomId: roomId,
            state: CreatureState.Dead
        );
        _context.Creatures.Add(corpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(WorldId, roomId, stateId, null);

        // Act
        var result = await _handler.Handle(
            new GetAllNearbyCreaturesQuery { Location = location },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, x => x.Id == corpse.Id);
    }

    [Fact]
    public async Task Handle_ReturnsCurrentAndMaximumHp()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var creature = Builders.MakeCreature(
            WorldId,
            stateId: stateId,
            currentHp: 5,
            roomId: roomId
        );
        // Simulate gear-boosted cached Maximum diverging from base Attributes.MaximumHp,
        // to prove the query reads the cached column rather than the base value.
        creature.MaximumHp += 50;
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(WorldId, roomId, stateId, null);

        // Act
        var result = await _handler.Handle(
            new GetAllNearbyCreaturesQuery { Location = location },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = Assert.Single(result, x => x.Id == creature.Id);
        Assert.Equal(5, summary.CurrentHp);
        Assert.Equal(creature.MaximumHp, summary.MaximumHp);
        Assert.NotEqual(creature.BaseAttributes.MaximumHp, summary.MaximumHp);
    }

    [Fact]
    public async Task Handle_ReturnsNullProfession_WhenCreatureHasNoProfession()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var minor = Builders.MakeCreature(
            WorldId,
            stateId: stateId,
            profession: null,
            roomId: roomId
        );
        _context.Creatures.Add(minor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(WorldId, roomId, stateId, null);

        // Act
        var result = await _handler.Handle(
            new GetAllNearbyCreaturesQuery { Location = location },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = Assert.Single(result, x => x.Id == minor.Id);
        Assert.Null(summary.Profession);
    }

    [Fact]
    public async Task Handle_ExcludesDeadCreatures_WhenIncludeDeadIsFalse()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var corpse = Builders.MakeCreature(
            WorldId,
            stateId: stateId,
            roomId: roomId,
            state: CreatureState.Dead
        );
        var alive = Builders.MakeCreature(WorldId, stateId: stateId, roomId: roomId);
        _context.Creatures.AddRange(corpse, alive);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(WorldId, roomId, stateId, null);

        // Act
        var result = await _handler.Handle(
            new GetAllNearbyCreaturesQuery { Location = location, IncludeDead = false },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(result, x => x.Id == corpse.Id);
        Assert.Contains(result, x => x.Id == alive.Id);
    }
}
