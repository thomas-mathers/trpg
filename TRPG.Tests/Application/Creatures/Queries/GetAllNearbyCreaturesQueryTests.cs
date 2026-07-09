using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetAllNearbyCreaturesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetAllNearbyCreaturesQueryHandler _handler = null!;
    private readonly Guid _worldId = Guid.NewGuid();

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
        var inRoom = Builders.MakeCreature(_worldId, stateId: stateId);
        inRoom.RoomId = roomId;
        var outdoors = Builders.MakeCreature(_worldId, stateId: stateId);
        _context.Creatures.AddRange(inRoom, outdoors);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(_worldId, roomId, stateId, null);

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
        var outdoors = Builders.MakeCreature(_worldId, stateId: stateId, districtId: districtId);
        var indoors = Builders.MakeCreature(_worldId, stateId: stateId, districtId: districtId);
        indoors.RoomId = Guid.NewGuid();
        _context.Creatures.AddRange(outdoors, indoors);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(_worldId, null, stateId, districtId);

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
        var player = Builders.MakeCreature(_worldId, stateId: stateId);
        player.RoomId = roomId;
        var other = Builders.MakeCreature(_worldId, stateId: stateId);
        other.RoomId = roomId;
        _context.Creatures.AddRange(player, other);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var location = new CreatureLocation(_worldId, roomId, stateId, null);

        // Act
        var result = await _handler.Handle(
            new GetAllNearbyCreaturesQuery { Location = location, ExcludingCreatureId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(result, x => x.Id == player.Id);
        Assert.Contains(result, x => x.Id == other.Id);
    }
}
