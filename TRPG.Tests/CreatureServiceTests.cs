using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class CreatureServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private CreatureService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new CreatureService(_context);

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Add_PersistsCreature() {
        // Arrange
        var creature = Builders.MakeCreature();

        // Act
        await _service.Add(creature, TestContext.Current.CancellationToken);

        // Assert
        var found = await _context.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Equal(creature.Name, found.Name);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsCreature_WhenExists() {
        // Act
        var result = await _service.GetById(_creature.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_creature.Id, result.Id);
    }

    [Fact]
    public async Task Update_SavesChanges() {
        // Arrange
        _creature.Gold = 500;

        // Act
        await _service.Update(_creature, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Creatures.FindAsync([_creature.Id], TestContext.Current.CancellationToken);
        Assert.Equal(500, updated!.Gold);
    }

    [Fact]
    public async Task Delete_RemovesCreature() {
        // Arrange
        var creature = Builders.MakeCreature();
        await _service.Add(creature, TestContext.Current.CancellationToken);

        // Act
        await _service.Delete(creature.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var found = await verifyContext.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.Null(found);
    }

    [Fact]
    public async Task GetAllInState_ReturnsOnlyCreaturesInState() {
        // Arrange
        var worldId = Guid.NewGuid();
        var stateId = Guid.NewGuid();

        var inState = Builders.MakeCreature(worldId, stateId: stateId);
        var differentState = Builders.MakeCreature(worldId);
        var otherWorld = Builders.MakeCreature(stateId: stateId);

        await _service.Add(inState, TestContext.Current.CancellationToken);
        await _service.Add(differentState, TestContext.Current.CancellationToken);
        await _service.Add(otherWorld, TestContext.Current.CancellationToken);

        // Act
        var results = await _service.GetAllInState(worldId, stateId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(results, p => p.Id == inState.Id);
        Assert.DoesNotContain(results, p => p.Id == differentState.Id);
        Assert.DoesNotContain(results, p => p.Id == otherWorld.Id);
    }

    [Fact]
    public async Task GetIdsByDistrict_ReturnsOnlyCreaturesInDistrict() {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();

        var inDistrict = Builders.MakeCreature(worldId, districtId: districtId);
        var differentDistrict = Builders.MakeCreature(worldId, districtId: Guid.NewGuid());
        var otherWorld = Builders.MakeCreature(districtId: districtId);

        await _service.Add(inDistrict, TestContext.Current.CancellationToken);
        await _service.Add(differentDistrict, TestContext.Current.CancellationToken);
        await _service.Add(otherWorld, TestContext.Current.CancellationToken);

        // Act
        var results = await _service.GetIdsByDistrict(worldId, districtId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(inDistrict.Id, results);
        Assert.DoesNotContain(differentDistrict.Id, results);
        Assert.DoesNotContain(otherWorld.Id, results);
    }

    [Fact]
    public async Task GetByNameOutdoorsInDistrict_ReturnsCreature_WhenInDistrict() {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var creature = Builders.MakeCreature(worldId, districtId: districtId);
        await _service.Add(creature, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetByNameOutdoorsInDistrict(worldId, districtId, creature.Name,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(creature.Id, result.Id);
    }

    [Fact]
    public async Task GetByNameOutdoorsInDistrict_ReturnsNull_WhenInDifferentDistrict() {
        // Arrange
        var worldId = Guid.NewGuid();
        var creature = Builders.MakeCreature(worldId, districtId: Guid.NewGuid());
        await _service.Add(creature, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetByNameOutdoorsInDistrict(worldId, Guid.NewGuid(), creature.Name,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByNameNearby_ReturnsCreature_WhenIndoors() {
        // Arrange
        var worldId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var target = Builders.MakeCreature(worldId);
        target.RoomId = roomId;
        await _service.Add(target, TestContext.Current.CancellationToken);
        var player = Builders.MakeCreature(worldId);
        player.RoomId = roomId;

        // Act
        var result =
            await _service.GetByNameNearby(worldId, player, target.Name, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }

    [Fact]
    public async Task GetByNameNearby_ScopesToDistrict_WhenOutdoorsInCity() {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var target = Builders.MakeCreature(worldId, districtId: districtId);
        await _service.Add(target, TestContext.Current.CancellationToken);
        var player = Builders.MakeCreature(worldId, districtId: districtId);

        // Act
        var result =
            await _service.GetByNameNearby(worldId, player, target.Name, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }

    [Fact]
    public async Task GetByNameNearby_ScopesToState_WhenOutdoorsWithNoCity() {
        // Arrange
        var worldId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var target = Builders.MakeCreature(worldId, stateId: stateId);
        await _service.Add(target, TestContext.Current.CancellationToken);
        var player = Builders.MakeCreature(worldId, stateId: stateId);

        // Act
        var result =
            await _service.GetByNameNearby(worldId, player, target.Name, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }

    [Fact]
    public async Task GetAllNearby_ReturnsCreaturesInRoom_WhenLocationHasRoomId() {
        // Arrange
        var worldId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var inRoom = Builders.MakeCreature(worldId, stateId: stateId);
        inRoom.RoomId = roomId;
        var outdoors = Builders.MakeCreature(worldId, stateId: stateId);
        await _service.Add(inRoom, TestContext.Current.CancellationToken);
        await _service.Add(outdoors, TestContext.Current.CancellationToken);
        var location = new CreatureLocation(worldId, roomId, stateId, null);

        // Act
        var result = await _service.GetAllNearby(location, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, x => x.Id == inRoom.Id);
        Assert.DoesNotContain(result, x => x.Id == outdoors.Id);
    }

    [Fact]
    public async Task GetAllNearby_ReturnsCreaturesOutdoors_WhenLocationHasNoRoomId() {
        // Arrange
        var worldId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var outdoors = Builders.MakeCreature(worldId, stateId: stateId, districtId: districtId);
        var indoors = Builders.MakeCreature(worldId, stateId: stateId, districtId: districtId);
        indoors.RoomId = Guid.NewGuid();
        await _service.Add(outdoors, TestContext.Current.CancellationToken);
        await _service.Add(indoors, TestContext.Current.CancellationToken);
        var location = new CreatureLocation(worldId, null, stateId, districtId);

        // Act
        var result = await _service.GetAllNearby(location, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, x => x.Id == outdoors.Id);
        Assert.DoesNotContain(result, x => x.Id == indoors.Id);
    }

    [Fact]
    public async Task GetAllNearby_ExcludesGivenCreature() {
        // Arrange
        var worldId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var player = Builders.MakeCreature(worldId, stateId: stateId);
        player.RoomId = roomId;
        var other = Builders.MakeCreature(worldId, stateId: stateId);
        other.RoomId = roomId;
        await _service.Add(player, TestContext.Current.CancellationToken);
        await _service.Add(other, TestContext.Current.CancellationToken);
        var location = new CreatureLocation(worldId, roomId, stateId, null);

        // Act
        var result = await _service.GetAllNearby(location, player.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(result, x => x.Id == player.Id);
        Assert.Contains(result, x => x.Id == other.Id);
    }
}
