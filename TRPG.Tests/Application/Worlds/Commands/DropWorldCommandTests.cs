using Microsoft.EntityFrameworkCore;
using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

[Collection("Database")]
public sealed class DropWorldCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid OtherWorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private DropWorldCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new DropWorldCommandHandler(_context);

        await SeedWorldData(WorldId);
        await SeedWorldData(OtherWorldId);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task SeedWorldData(Guid worldId)
    {
        var creature = Builders.MakeCreature(worldId);
        var faction = Builders.MakeFaction(worldId);
        var building = Builders.MakeBuilding(Guid.NewGuid(), worldId: worldId);
        var room = Builders.MakeRoom(building.Id, worldId: worldId);
        var bed = new Bed
        {
            RoomId = room.Id,
            Name = "Bed",
            Description = "A test bed.",
            WorldId = worldId,
        };
        var item = Builders.MakeItem(worldId);
        var factionMember = new FactionMember
        {
            FactionId = faction.Id,
            CreatureId = creature.Id,
            Role = FactionRole.Member,
            WorldId = worldId,
        };

        _context.Creatures.Add(creature);
        _context.Factions.Add(faction);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Props.Add(bed);
        _context.Items.Add(item);
        _context.FactionMembers.Add(factionMember);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_DeletesEveryTableForTheWorld_AndLeavesOtherWorldsUntouched()
    {
        // Act
        await _handler.Handle(
            new DropWorldCommand { WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();

        Assert.False(
            await verifyContext.Creatures.AnyAsync(
                x => x.WorldId == WorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verifyContext.Factions.AnyAsync(
                x => x.WorldId == WorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verifyContext.Buildings.AnyAsync(
                x => x.WorldId == WorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verifyContext.Rooms.AnyAsync(
                x => x.WorldId == WorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verifyContext.Props.AnyAsync(
                x => x.WorldId == WorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verifyContext.Items.AnyAsync(
                x => x.WorldId == WorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verifyContext.FactionMembers.AnyAsync(
                x => x.WorldId == WorldId,
                TestContext.Current.CancellationToken
            )
        );

        Assert.True(
            await verifyContext.Creatures.AnyAsync(
                x => x.WorldId == OtherWorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.Factions.AnyAsync(
                x => x.WorldId == OtherWorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.Buildings.AnyAsync(
                x => x.WorldId == OtherWorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.Rooms.AnyAsync(
                x => x.WorldId == OtherWorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.Props.AnyAsync(
                x => x.WorldId == OtherWorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.Items.AnyAsync(
                x => x.WorldId == OtherWorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.FactionMembers.AnyAsync(
                x => x.WorldId == OtherWorldId,
                TestContext.Current.CancellationToken
            )
        );
    }
}
