using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class SkillServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private PersonService _personService = null!;
    private SkillService _skillService = null!;
    private Person _person = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _personService = new PersonService(_context);
        _skillService = new SkillService(_context);
        _person = Builders.MakePerson();
        await _personService.Add(_person);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task AddSkill_AddsSkillToPersonWithNoPrerequisites()
    {
        // Arrange
        var skill = Builders.MakeSkill();
        _context.Skills.Add(skill);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _skillService.AddSkill(_person.Id, skill.Id, TestContext.Current.CancellationToken);

        // Assert
        var skills = await _skillService.GetAllByPersonId(_person.Id, TestContext.Current.CancellationToken);
        Assert.Single(skills);
        Assert.Equal(skill.Id, skills[0].SkillId);
    }

    [Fact]
    public async Task AddSkill_Throws_WhenPrerequisiteNotMet()
    {
        // Arrange
        var prereq = Builders.MakeSkill();
        var skill = Builders.MakeSkill();
        _context.Skills.AddRange(prereq, skill);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _context.SkillPrerequisites.Add(new SkillPrerequisite { SkillId = skill.Id, PrerequisiteSkillId = prereq.Id });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _skillService.AddSkill(_person.Id, skill.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddSkill_Succeeds_WhenPrerequisiteMet()
    {
        // Arrange
        var prereq = Builders.MakeSkill();
        var skill = Builders.MakeSkill();
        _context.Skills.AddRange(prereq, skill);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _context.SkillPrerequisites.Add(new SkillPrerequisite { SkillId = skill.Id, PrerequisiteSkillId = prereq.Id });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _skillService.AddSkill(_person.Id, prereq.Id, TestContext.Current.CancellationToken);

        // Act
        await _skillService.AddSkill(_person.Id, skill.Id, TestContext.Current.CancellationToken);

        // Assert
        var skills = await _skillService.GetAllByPersonId(_person.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, skills.Count);
    }

    [Fact]
    public async Task RemoveSkill_RemovesPersonSkill()
    {
        // Arrange
        var skill = Builders.MakeSkill();
        _context.Skills.Add(skill);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _skillService.AddSkill(_person.Id, skill.Id, TestContext.Current.CancellationToken);

        // Act
        await _skillService.RemoveSkill(_person.Id, skill.Id, TestContext.Current.CancellationToken);

        // Assert
        var skills = await _skillService.GetAllByPersonId(_person.Id, TestContext.Current.CancellationToken);
        Assert.Empty(skills);
    }

    [Fact]
    public async Task GetAllPrerequisites_ReturnsCorrectIds()
    {
        // Arrange
        var prereq1 = Builders.MakeSkill();
        var prereq2 = Builders.MakeSkill();
        var skill = Builders.MakeSkill();
        _context.Skills.AddRange(prereq1, prereq2, skill);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _context.SkillPrerequisites.AddRange(
            new SkillPrerequisite { SkillId = skill.Id, PrerequisiteSkillId = prereq1.Id },
            new SkillPrerequisite { SkillId = skill.Id, PrerequisiteSkillId = prereq2.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var prereqs = await _skillService.GetAllPrerequisites(skill.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, prereqs.Count);
        Assert.Contains(prereq1.Id, prereqs);
        Assert.Contains(prereq2.Id, prereqs);
    }
}
