using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class CreateEncounterGroupsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CreateEncounterGroupsCommandHandler _handler = null!;
    private readonly Faction _faction = Builders.MakeFaction(WorldId);
    private readonly Creature _monster = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<CreateEncounterGroupsCommandHandler>();

        _context.Factions.Add(_faction);
        _context.Creatures.Add(_monster);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesGroupAndItsMembers()
    {
        // Arrange
        var group = Builders.MakeEncounterGroup(WorldId, LocationId, _faction.Id);
        var member = Builders.MakeEncounterGroupMember(WorldId, group.Id, _monster.Id);

        // Act
        await _handler.Handle(
            new CreateEncounterGroupsCommand { Groups = [group], Members = [member] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.EncounterGroups.AnyAsync(
                g => g.Id == group.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.EncounterGroupMembers.AnyAsync(
                m => m.CreatureId == member.CreatureId && m.EncounterGroupId == group.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
