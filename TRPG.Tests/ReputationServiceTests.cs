using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class ReputationServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private Guid _creatureId;
    private Faction _faction = null!;
    private ReputationService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new ReputationService(_context);

        _faction = Builders.MakeFaction();
        var creature = Builders.MakeCreature();
        _creatureId = creature.Id;
        _context.Factions.Add(_faction);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedCreature() {
        var creature = Builders.MakeCreature();
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    [Fact]
    public async Task AdjustReputation_CreatesReputation_WhenFirstCall() {
        // Act
        await _service.AdjustReputation(_creatureId, _faction.Id, ReputationTargetType.Faction, 10,
            TestContext.Current.CancellationToken);

        // Assert
        var reputations = await _service.GetAllByCreatureId(_creatureId, TestContext.Current.CancellationToken);
        Assert.Single(reputations);
        Assert.Equal(10, reputations.First().Score);
    }

    [Fact]
    public async Task AdjustReputation_IncrementsScore_WhenSubsequentCall() {
        // Arrange
        var creatureId = (await SeedCreature()).Id;
        await _service.AdjustReputation(creatureId, _faction.Id, ReputationTargetType.Faction, 10,
            TestContext.Current.CancellationToken);

        // Act
        await _service.AdjustReputation(creatureId, _faction.Id, ReputationTargetType.Faction, 5,
            TestContext.Current.CancellationToken);

        // Assert
        var reputations = await _service.GetAllByCreatureId(creatureId, TestContext.Current.CancellationToken);
        Assert.Single(reputations);
        Assert.Equal(15, reputations.First().Score);
    }

    [Fact]
    public async Task AdjustReputation_SupportsNegativeDelta() {
        // Arrange
        var creatureId = (await SeedCreature()).Id;
        await _service.AdjustReputation(creatureId, _faction.Id, ReputationTargetType.Faction, 20,
            TestContext.Current.CancellationToken);

        // Act
        await _service.AdjustReputation(creatureId, _faction.Id, ReputationTargetType.Faction, -8,
            TestContext.Current.CancellationToken);

        // Assert
        var reputations = await _service.GetAllByCreatureId(creatureId, TestContext.Current.CancellationToken);
        Assert.Equal(12, reputations.First().Score);
    }

    [Fact]
    public async Task AdjustReputation_Throws_WhenFactionTargetDoesNotExist() {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AdjustReputation(_creatureId, Guid.NewGuid(), ReputationTargetType.Faction, 10,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdjustReputation_Throws_WhenCreatureTargetDoesNotExist() {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AdjustReputation(_creatureId, Guid.NewGuid(), ReputationTargetType.Creature, 10,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAllByCreatureId_ReturnsReputationsForCreature() {
        // Arrange
        var creatureId = (await SeedCreature()).Id;
        var faction2 = Builders.MakeFaction();
        _context.Factions.Add(faction2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _service.AdjustReputation(creatureId, _faction.Id, ReputationTargetType.Faction, 5,
            TestContext.Current.CancellationToken);
        await _service.AdjustReputation(creatureId, faction2.Id, ReputationTargetType.Faction, 10,
            TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetAllByCreatureId(creatureId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result,
            r => r.TargetId == _faction.Id && r.TargetType == ReputationTargetType.Faction && r.Score == 5);
        Assert.Contains(result,
            r => r.TargetId == faction2.Id && r.TargetType == ReputationTargetType.Faction && r.Score == 10);
    }

    [Fact]
    public async Task GetEffectiveReputation_ReturnsZero_WhenNoReputationHistory() {
        // Arrange
        var npc = Builders.MakeCreature();
        _context.Creatures.Add(npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetEffectiveReputation(_creatureId, npc.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetEffectiveReputation_SumsFactionAndCreaturePersonalReputation() {
        // Arrange — an NPC belonging to two factions, plus a personal reputation row
        var npc = Builders.MakeCreature();
        var guildFaction = Builders.MakeFaction();
        _context.Creatures.Add(npc);
        _context.Factions.Add(guildFaction);
        _context.FactionMembers.AddRange(
            new FactionMember { FactionId = _faction.Id, CreatureId = npc.Id, Role = FactionRole.Member },
            new FactionMember { FactionId = guildFaction.Id, CreatureId = npc.Id, Role = FactionRole.Member }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.AdjustReputation(_creatureId, _faction.Id, ReputationTargetType.Faction, 5,
            TestContext.Current.CancellationToken);
        await _service.AdjustReputation(_creatureId, guildFaction.Id, ReputationTargetType.Faction, 10,
            TestContext.Current.CancellationToken);
        await _service.AdjustReputation(_creatureId, npc.Id, ReputationTargetType.Creature, 3,
            TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetEffectiveReputation(_creatureId, npc.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(18, result);
    }
}
