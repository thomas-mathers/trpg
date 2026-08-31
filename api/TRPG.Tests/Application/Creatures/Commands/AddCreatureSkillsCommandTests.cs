using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class AddCreatureSkillsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AddCreatureSkillsCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AddCreatureSkillsCommandHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_AddsAllSkills()
    {
        // Arrange
        var melee = Builders.MakeCreatureSkill(_creature.Id, Skill.Melee, level: 2);
        var archery = Builders.MakeCreatureSkill(_creature.Id, Skill.Archery, level: 3);

        // Act
        await _handler.Handle(
            new AddCreatureSkillsCommand { Skills = [melee, archery] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var skills = await verifyContext
            .CreatureSkills.Where(skill => skill.CreatureId == _creature.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, skills.Count);
        Assert.Contains(skills, skill => skill.Skill == Skill.Melee && skill.Level == 2);
        Assert.Contains(skills, skill => skill.Skill == Skill.Archery && skill.Level == 3);
    }
}
