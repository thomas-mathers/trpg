using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Commands;

[Collection("Database")]
public sealed class CompleteQuestCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CompleteQuestCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _giver = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<CompleteQuestCommandHandler>();
        _context.Creatures.AddRange(_player, _giver);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CompletesQuestAndAddsGold_WhenQuestIsReady()
    {
        // Arrange
        var creatureQuest = await SeedQuest(QuestStatus.ReadyToComplete);
        creatureQuest.IsTracked = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CompleteQuestCommand
            {
                PlayerId = _player.Id,
                QuestId = creatureQuest.QuestId,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(QuestStatus.Completed, creatureQuest.Status);
        Assert.False(creatureQuest.IsTracked);
        var gold = await _context
            .Items.OfType<Gold>()
            .SingleAsync(
                item =>
                    item.Ownership.OwnerId == _player.Id
                    && item.Ownership.OwnerType == OwnerType.Creature,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(creatureQuest.Quest.GoldReward, gold.Quantity);
        Assert.Equal(_player.Id, gold.Ownership.OwnerId);
    }

    [Fact]
    public async Task Handle_Throws_WhenQuestIsNotReady()
    {
        // Arrange
        var creatureQuest = await SeedQuest(QuestStatus.Accepted);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new CompleteQuestCommand
                {
                    PlayerId = _player.Id,
                    QuestId = creatureQuest.QuestId,
                    WorldId = WorldId,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_Throws_WhenPlayerDoesNotOwnRequiredQuestItem()
    {
        // Arrange
        var creatureQuest = await SeedQuest(QuestStatus.ReadyToComplete);
        _context.QuestObjectives.Add(
            new CollectItemObjective
            {
                QuestId = creatureQuest.QuestId,
                WorldId = WorldId,
                Name = "Recover item",
                Description = "Recover item",
                ItemId = Guid.NewGuid(),
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new CompleteQuestCommand
                {
                    PlayerId = _player.Id,
                    QuestId = creatureQuest.QuestId,
                    WorldId = WorldId,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_AppliesReputationRewards_WhenQuestIsCompleted()
    {
        // Arrange
        var firstFaction = Builders.MakeFaction(WorldId);
        var secondFaction = Builders.MakeFaction(WorldId);
        _context.Factions.AddRange(firstFaction, secondFaction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var quest = Builders.MakeQuest(_giver.Id, WorldId);
        var creatureQuest = await SeedQuest(
            QuestStatus.ReadyToComplete,
            quest,
            [
                new QuestReputationReward
                {
                    WorldId = WorldId,
                    QuestId = quest.Id,
                    TargetId = _giver.Id,
                    TargetType = ReputationTargetType.Creature,
                    Score = 10,
                },
                new QuestReputationReward
                {
                    WorldId = WorldId,
                    QuestId = quest.Id,
                    TargetId = firstFaction.Id,
                    TargetType = ReputationTargetType.Faction,
                    Score = 4,
                },
                new QuestReputationReward
                {
                    WorldId = WorldId,
                    QuestId = quest.Id,
                    TargetId = secondFaction.Id,
                    TargetType = ReputationTargetType.Faction,
                    Score = 7,
                },
            ]
        );

        // Act
        await _handler.Handle(
            new CompleteQuestCommand
            {
                PlayerId = _player.Id,
                QuestId = creatureQuest.QuestId,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _context
            .Reputations.Where(reputation => reputation.CreatureId == _player.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(10, reputations.Single(reputation => reputation.TargetId == _giver.Id).Score);
        Assert.Equal(
            4,
            reputations.Single(reputation => reputation.TargetId == firstFaction.Id).Score
        );
        Assert.Equal(
            7,
            reputations.Single(reputation => reputation.TargetId == secondFaction.Id).Score
        );
    }

    private async Task<CreatureQuest> SeedQuest(
        QuestStatus status,
        Quest? quest = null,
        IReadOnlyCollection<QuestReputationReward>? reputationRewards = null
    )
    {
        quest ??= Builders.MakeQuest(_giver.Id, WorldId);
        quest.ReputationRewards.AddRange(reputationRewards ?? []);
        var creatureQuest = new CreatureQuest
        {
            CreatureId = _player.Id,
            QuestId = quest.Id,
            Quest = quest,
            Status = status,
            WorldId = WorldId,
        };
        _context.CreatureQuests.Add(creatureQuest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creatureQuest;
    }
}
