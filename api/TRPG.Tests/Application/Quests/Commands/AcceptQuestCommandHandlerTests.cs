using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Commands;

[Collection("Database")]
public sealed class AcceptQuestCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AcceptQuestCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _giver = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AcceptQuestCommandHandler>();
        _context.Creatures.AddRange(_player, _giver);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesTrackedQuestAndObjectiveProgress_WhenPrerequisitesAreMet()
    {
        // Arrange
        var quest = Builders.MakeQuest(_giver.Id, WorldId);
        var objective = new CollectItemObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            ItemId = Guid.NewGuid(),
        };
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new AcceptQuestCommand
            {
                PlayerId = _player.Id,
                QuestId = quest.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var creatureQuest = await _context.CreatureQuests.SingleAsync(
            creatureQuest =>
                creatureQuest.CreatureId == _player.Id && creatureQuest.QuestId == quest.Id,
            TestContext.Current.CancellationToken
        );
        var progress = await _context.CreatureQuestObjectives.SingleAsync(
            progress => progress.CreatureId == _player.Id && progress.ObjectiveId == objective.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(QuestStatus.Accepted, creatureQuest.Status);
        Assert.True(creatureQuest.IsTracked);
        Assert.Equal(objective.Id, progress.ObjectiveId);
        Assert.Equal(0, progress.Amount);
    }

    [Fact]
    public async Task Handle_Throws_WhenPrerequisiteIsNotCompleted()
    {
        // Arrange
        var prerequisite = Builders.MakeQuest(_giver.Id, WorldId);
        var quest = Builders.MakeQuest(_giver.Id, WorldId);
        quest.PrerequisiteQuestIds.Add(prerequisite.Id);
        _context.Quests.AddRange(prerequisite, quest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new AcceptQuestCommand
                {
                    PlayerId = _player.Id,
                    QuestId = quest.Id,
                    WorldId = WorldId,
                },
                TestContext.Current.CancellationToken
            )
        );
    }
}
