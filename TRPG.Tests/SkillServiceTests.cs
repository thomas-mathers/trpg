using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class SkillServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private PersonService _personService = null!;
    private SkillService _skillService = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _personService = new PersonService(_context);
        _skillService = new SkillService(_context);
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task AddSkill_AddsSkillToPersonWithNoPrerequisites()
    {
        // Arrange
        var person = Builders.MakePerson();
        await _personService.Add(person);

        var skill = Builders.MakeSkill();
        _context.Skills.Add(skill);
        await _context.SaveChangesAsync();

        // Act
        await _skillService.AddSkill(person.Id, skill.Id);

        // Assert
        var skills = await _skillService.GetAllByPersonId(person.Id);
        Assert.Single(skills);
        Assert.Equal(skill.Id, skills[0].SkillId);
    }

    [Fact]
    public async Task AddSkill_Throws_WhenPrerequisiteNotMet()
    {
        // Arrange
        var person = Builders.MakePerson();
        await _personService.Add(person);

        var prereq = Builders.MakeSkill();
        var skill = Builders.MakeSkill();
        _context.Skills.AddRange(prereq, skill);
        await _context.SaveChangesAsync();

        _context.SkillPrerequisites.Add(new SkillPrerequisite { SkillId = skill.Id, PrerequisiteSkillId = prereq.Id });
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _skillService.AddSkill(person.Id, skill.Id));
    }

    [Fact]
    public async Task AddSkill_Succeeds_WhenPrerequisiteMet()
    {
        // Arrange
        var person = Builders.MakePerson();
        await _personService.Add(person);

        var prereq = Builders.MakeSkill();
        var skill = Builders.MakeSkill();
        _context.Skills.AddRange(prereq, skill);
        await _context.SaveChangesAsync();

        _context.SkillPrerequisites.Add(new SkillPrerequisite { SkillId = skill.Id, PrerequisiteSkillId = prereq.Id });
        await _context.SaveChangesAsync();

        await _skillService.AddSkill(person.Id, prereq.Id);

        // Act
        await _skillService.AddSkill(person.Id, skill.Id);

        // Assert
        var skills = await _skillService.GetAllByPersonId(person.Id);
        Assert.Equal(2, skills.Count);
    }

    [Fact]
    public async Task RemoveSkill_RemovesPersonSkill()
    {
        // Arrange
        var person = Builders.MakePerson();
        await _personService.Add(person);

        var skill = Builders.MakeSkill();
        _context.Skills.Add(skill);
        await _context.SaveChangesAsync();

        await _skillService.AddSkill(person.Id, skill.Id);

        // Act
        await _skillService.RemoveSkill(person.Id, skill.Id);

        // Assert
        var skills = await _skillService.GetAllByPersonId(person.Id);
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
        await _context.SaveChangesAsync();

        _context.SkillPrerequisites.AddRange(
            new SkillPrerequisite { SkillId = skill.Id, PrerequisiteSkillId = prereq1.Id },
            new SkillPrerequisite { SkillId = skill.Id, PrerequisiteSkillId = prereq2.Id }
        );
        await _context.SaveChangesAsync();

        // Act
        var prereqs = await _skillService.GetAllPrerequisites(skill.Id);

        // Assert
        Assert.Equal(2, prereqs.Count);
        Assert.Contains(prereq1.Id, prereqs);
        Assert.Contains(prereq2.Id, prereqs);
    }
}
