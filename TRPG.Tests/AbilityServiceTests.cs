using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class AbilityServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private AbilityService _abilityService = null!;
    private TrpgDbContext _context = null!;
    private Person _person = null!;
    private PersonService _personService = null!;
    private SkillService _skillService = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _personService = new PersonService(_context);
        _abilityService = new AbilityService(_context);
        _skillService = new SkillService(_context);
        _person = Builders.MakePerson();
        await _personService.Add(_person);
        await _skillService.AddExperience(_person.Id, Skill.Swordsmanship, 250);
        await _skillService.AddExperience(_person.Id, Skill.Stealth, 250);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task AddAbility_AddsAbilityToPersonWithNoPrerequisites() {
        // Act
        await _abilityService.AddAbility(_person.Id, "Slash", TestContext.Current.CancellationToken);

        // Assert
        var abilities = await _abilityService.GetAllByPersonId(_person.Id, TestContext.Current.CancellationToken);
        Assert.Single(abilities);
        Assert.Equal("Slash", abilities[0].AbilityName);
    }

    [Fact]
    public async Task AddAbility_Throws_WhenSkillLevelTooLow() {
        // Act & Assert — Whirlwind requires Swordsmanship level 5, person has level 1
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _abilityService.AddAbility(_person.Id, "Whirlwind", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAbility_Throws_WhenPrerequisiteNotMet() {
        // Act & Assert — Cleave requires Slash first
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _abilityService.AddAbility(_person.Id, "Cleave", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAbility_Succeeds_WhenPrerequisiteMet() {
        // Arrange
        await _abilityService.AddAbility(_person.Id, "Slash", TestContext.Current.CancellationToken);

        // Act
        await _abilityService.AddAbility(_person.Id, "Cleave", TestContext.Current.CancellationToken);

        // Assert
        var abilities = await _abilityService.GetAllByPersonId(_person.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, abilities.Count);
    }

    [Fact]
    public async Task RemoveAbility_RemovesPersonAbility() {
        // Arrange
        await _abilityService.AddAbility(_person.Id, "Slash", TestContext.Current.CancellationToken);

        // Act
        await _abilityService.RemoveAbility(_person.Id, "Slash", TestContext.Current.CancellationToken);

        // Assert
        var abilities = await _abilityService.GetAllByPersonId(_person.Id, TestContext.Current.CancellationToken);
        Assert.Empty(abilities);
    }
}
