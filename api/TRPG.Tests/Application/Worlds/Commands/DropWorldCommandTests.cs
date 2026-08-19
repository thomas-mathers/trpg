using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

[Collection("Database")]
public sealed class DropWorldCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid OtherWorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private DropWorldCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<DropWorldCommandHandler>();

        await SeedWorldData(WorldId);
        await SeedWorldData(OtherWorldId);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    private async Task SeedWorldData(Guid worldId)
    {
        var creature = Builders.MakeCreature(worldId);
        var faction = Builders.MakeFaction(worldId);
        var building = Builders.MakeBuilding(worldId: worldId);
        var room = Builders.MakeRoom(building.Id, worldId: worldId);
        var location = Builders.MakeLocation(worldId, roomId: room.Id);
        var bed = new Bed
        {
            LocationId = room.LocationId,
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
        var weaponProficiency = new CreatureWeaponProficiency
        {
            WorldId = worldId,
            CreatureId = creature.Id,
            WeaponType = WeaponType.Sword,
            Proficiency = 3,
        };
        var encounterGroup = Builders.MakeEncounterGroup(worldId, location.Id, faction.Id);
        var encounterGroupMember = Builders.MakeEncounterGroupMember(
            worldId,
            encounterGroup.Id,
            creature.Id
        );
        var encounter = Builders.MakeHostileEncounter(worldId, creature.Id, location.Id);

        _context.Creatures.Add(creature);
        _context.Factions.Add(faction);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.Props.Add(bed);
        _context.Items.Add(item);
        _context.FactionMembers.Add(factionMember);
        _context.CreatureWeaponProficiencies.Add(weaponProficiency);
        _context.EncounterGroups.Add(encounterGroup);
        _context.EncounterGroupMembers.Add(encounterGroupMember);
        _context.Encounters.Add(encounter);
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
        await AssertWorldDataExists(WorldId, expected: false);
        await AssertWorldDataExists(OtherWorldId, expected: true);
    }

    private async Task AssertWorldDataExists(Guid worldId, bool expected)
    {
        await using var verifyContext = db.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(
            expected,
            await verifyContext.Creatures.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Factions.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Buildings.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Rooms.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Locations.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Props.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Items.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.FactionMembers.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.CreatureWeaponProficiencies.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.EncounterGroups.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.EncounterGroupMembers.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.Encounters.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
    }
}
