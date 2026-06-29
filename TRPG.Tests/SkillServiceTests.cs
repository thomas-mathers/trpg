using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class SkillServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private Person _person = null!;
    private SkillService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new SkillService(_context);
        _person = Builders.MakePerson();
        _context.Persons.Add(_person);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task AddExperience_CreatesPersonSkill_WhenNoneExists() {
        // Act
        await _service.AddExperience(_person.Id, Skill.Archery, 50, TestContext.Current.CancellationToken);

        // Assert
        var skills = await _service.GetAllByPersonId(_person.Id, TestContext.Current.CancellationToken);
        Assert.Single(skills);
        Assert.Equal(Skill.Archery, skills[0].Skill);
        Assert.Equal(50, skills[0].Experience);
    }

    [Fact]
    public async Task AddExperience_AccumulatesOnSubsequentCalls() {
        // Arrange
        await _service.AddExperience(_person.Id, Skill.Archery, 50, TestContext.Current.CancellationToken);

        // Act
        await _service.AddExperience(_person.Id, Skill.Archery, 60, TestContext.Current.CancellationToken);

        // Assert
        var skills = await _service.GetAllByPersonId(_person.Id, TestContext.Current.CancellationToken);
        Assert.Equal(110, skills[0].Experience);
    }

    [Fact]
    public async Task AddExperience_BumpsLevel_WhenThresholdCrossed() {
        // Act — 100 XP crosses the level 1 threshold
        var result = await _service.AddExperience(_person.Id, Skill.Archery, 100, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.Level);
    }

    [Fact]
    public async Task AddExperience_DoesNotBumpLevel_WhenBelowThreshold() {
        // Act
        var result = await _service.AddExperience(_person.Id, Skill.Archery, 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.Level);
    }

    [Fact]
    public async Task GetAllByPersonId_ReturnsAllSkills() {
        // Arrange
        await _service.AddExperience(_person.Id, Skill.Archery, 100, TestContext.Current.CancellationToken);
        await _service.AddExperience(_person.Id, Skill.Stealth, 100, TestContext.Current.CancellationToken);

        // Act
        var skills = await _service.GetAllByPersonId(_person.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, skills.Count);
        Assert.Contains(skills, s => s.Skill == Skill.Archery);
        Assert.Contains(skills, s => s.Skill == Skill.Stealth);
    }
}
