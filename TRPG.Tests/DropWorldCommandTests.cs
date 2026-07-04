using Microsoft.EntityFrameworkCore;
using TRPG.Commands;
using TRPG.Data;
using TRPG.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class DropWorldCommandTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private DropWorldCommandHandler _handler = null!;
    private Guid _worldId;
    private Guid _otherWorldId;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _handler = new DropWorldCommandHandler(_context);

        _worldId = Guid.NewGuid();
        _otherWorldId = Guid.NewGuid();

        await SeedWorldData(_worldId);
        await SeedWorldData(_otherWorldId);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    private async Task SeedWorldData(Guid worldId) {
        var person = Builders.MakePerson(worldId: worldId);
        var faction = Builders.MakeFaction(worldId: worldId);
        var building = Builders.MakeBuilding(Guid.NewGuid(), worldId: worldId);
        var room = Builders.MakeRoom(building.Id, worldId: worldId);
        var bed = new Bed { RoomId = room.Id, Name = "Bed", Description = "A test bed.", WorldId = worldId };
        var item = Builders.MakeItem(worldId: worldId);
        var factionMember = new FactionMember
            { FactionId = faction.Id, PersonId = person.Id, Role = FactionRole.Member, WorldId = worldId };

        _context.Persons.Add(person);
        _context.Factions.Add(faction);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Props.Add(bed);
        _context.Items.Add(item);
        _context.FactionMembers.Add(factionMember);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_DeletesEveryTableForTheWorld_AndLeavesOtherWorldsUntouched() {
        // Act
        await _handler.Handle(new DropWorldCommand { WorldId = _worldId }, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();

        Assert.False(await verifyContext.Persons.AnyAsync(x => x.WorldId == _worldId, TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.Factions.AnyAsync(x => x.WorldId == _worldId, TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.Buildings.AnyAsync(x => x.WorldId == _worldId, TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.Rooms.AnyAsync(x => x.WorldId == _worldId, TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.Props.AnyAsync(x => x.WorldId == _worldId, TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.Items.AnyAsync(x => x.WorldId == _worldId, TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.FactionMembers.AnyAsync(x => x.WorldId == _worldId, TestContext.Current.CancellationToken));

        Assert.True(await verifyContext.Persons.AnyAsync(x => x.WorldId == _otherWorldId, TestContext.Current.CancellationToken));
        Assert.True(await verifyContext.Factions.AnyAsync(x => x.WorldId == _otherWorldId, TestContext.Current.CancellationToken));
        Assert.True(await verifyContext.Buildings.AnyAsync(x => x.WorldId == _otherWorldId, TestContext.Current.CancellationToken));
        Assert.True(await verifyContext.Rooms.AnyAsync(x => x.WorldId == _otherWorldId, TestContext.Current.CancellationToken));
        Assert.True(await verifyContext.Props.AnyAsync(x => x.WorldId == _otherWorldId, TestContext.Current.CancellationToken));
        Assert.True(await verifyContext.Items.AnyAsync(x => x.WorldId == _otherWorldId, TestContext.Current.CancellationToken));
        Assert.True(await verifyContext.FactionMembers.AnyAsync(x => x.WorldId == _otherWorldId, TestContext.Current.CancellationToken));
    }
}
