using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class ReputationServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private Guid _personId;
    private TrpgDbContext _context = null!;
    private ReputationService _service = null!;
    private Faction _faction = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new ReputationService(_context);

        _faction = Builders.MakeFaction();
        var person = Builders.MakePerson();
        _personId = person.Id;
        _context.Factions.Add(_faction);
        _context.Persons.Add(person);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Person> SeedPerson() {
        var person = Builders.MakePerson();
        _context.Persons.Add(person);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return person;
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AdjustReputation_CreatesReputation_WhenFirstCall() {
        // Act
        await _service.AdjustReputation(_personId, _faction.Id, ReputationTargetType.Faction, 10, TestContext.Current.CancellationToken);

        // Assert
        var reputations = await _service.GetAllByPersonId(_personId, TestContext.Current.CancellationToken);
        Assert.Single(reputations);
        Assert.Equal(10, reputations.First().Score);
    }

    [Fact]
    public async Task AdjustReputation_IncrementsScore_WhenSubsequentCall() {
        // Arrange
        var personId = (await SeedPerson()).Id;
        await _service.AdjustReputation(personId, _faction.Id, ReputationTargetType.Faction, 10, TestContext.Current.CancellationToken);

        // Act
        await _service.AdjustReputation(personId, _faction.Id, ReputationTargetType.Faction, 5, TestContext.Current.CancellationToken);

        // Assert
        var reputations = await _service.GetAllByPersonId(personId, TestContext.Current.CancellationToken);
        Assert.Single(reputations);
        Assert.Equal(15, reputations.First().Score);
    }

    [Fact]
    public async Task AdjustReputation_SupportsNegativeDelta() {
        // Arrange
        var personId = (await SeedPerson()).Id;
        await _service.AdjustReputation(personId, _faction.Id, ReputationTargetType.Faction, 20, TestContext.Current.CancellationToken);

        // Act
        await _service.AdjustReputation(personId, _faction.Id, ReputationTargetType.Faction, -8, TestContext.Current.CancellationToken);

        // Assert
        var reputations = await _service.GetAllByPersonId(personId, TestContext.Current.CancellationToken);
        Assert.Equal(12, reputations.First().Score);
    }

    [Fact]
    public async Task AdjustReputation_Throws_WhenFactionTargetDoesNotExist() {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AdjustReputation(_personId, Guid.NewGuid(), ReputationTargetType.Faction, 10, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdjustReputation_Throws_WhenPersonTargetDoesNotExist() {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AdjustReputation(_personId, Guid.NewGuid(), ReputationTargetType.Person, 10, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAllByPersonId_ReturnsReputationsForPerson() {
        // Arrange
        var personId = (await SeedPerson()).Id;
        var faction2 = Builders.MakeFaction();
        _context.Factions.Add(faction2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _service.AdjustReputation(personId, _faction.Id, ReputationTargetType.Faction, 5, TestContext.Current.CancellationToken);
        await _service.AdjustReputation(personId, faction2.Id, ReputationTargetType.Faction, 10, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetAllByPersonId(personId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.TargetId == _faction.Id && r.TargetType == ReputationTargetType.Faction && r.Score == 5);
        Assert.Contains(result, r => r.TargetId == faction2.Id && r.TargetType == ReputationTargetType.Faction && r.Score == 10);
    }

    [Fact]
    public async Task GetEffectiveReputation_ReturnsZero_WhenNoReputationHistory() {
        // Arrange
        var npc = Builders.MakePerson();
        _context.Persons.Add(npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetEffectiveReputation(_personId, npc.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetEffectiveReputation_SumsFactionAndPersonalReputation() {
        // Arrange — an NPC belonging to two factions, plus a personal reputation row
        var npc = Builders.MakePerson();
        var guildFaction = Builders.MakeFaction();
        _context.Persons.Add(npc);
        _context.Factions.Add(guildFaction);
        _context.FactionMembers.AddRange(
            new FactionMember { FactionId = _faction.Id, PersonId = npc.Id, Role = FactionRole.Member },
            new FactionMember { FactionId = guildFaction.Id, PersonId = npc.Id, Role = FactionRole.Member }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.AdjustReputation(_personId, _faction.Id, ReputationTargetType.Faction, 5, TestContext.Current.CancellationToken);
        await _service.AdjustReputation(_personId, guildFaction.Id, ReputationTargetType.Faction, 10, TestContext.Current.CancellationToken);
        await _service.AdjustReputation(_personId, npc.Id, ReputationTargetType.Person, 3, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetEffectiveReputation(_personId, npc.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(18, result);
    }
}
