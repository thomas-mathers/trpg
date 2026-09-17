using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Events;
using TRPG.Application.Quests.Results;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Commands;

public sealed class FactDisclosureCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AskAboutFactCommandHandler _askHandler = null!;
    private OfferBribeForFactCommandHandler _bribeHandler = null!;
    private IntimidateForFactCommandHandler _intimidateHandler = null!;
    private ICommandHandler<CompleteQuestCommand> _completeQuestHandler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, level: 5);
    private readonly Creature _npc = Builders.MakeCreature(WorldId, level: 5);
    private readonly Fact _fact = new()
    {
        WorldId = WorldId,
        Subject = "why the mine closed",
        Value = "A cave-in buried the lower tunnels.",
    };

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _askHandler = _serviceProvider.GetRequiredService<AskAboutFactCommandHandler>();
        _bribeHandler = _serviceProvider.GetRequiredService<OfferBribeForFactCommandHandler>();
        _intimidateHandler = _serviceProvider.GetRequiredService<IntimidateForFactCommandHandler>();
        _completeQuestHandler = _serviceProvider.GetRequiredService<
            ICommandHandler<CompleteQuestCommand>
        >();

        _context.Creatures.AddRange(_player, _npc);
        _context.Facts.Add(_fact);
        _context.Items.Add(Builders.MakeGold(WorldId, quantity: 10_000, ownerId: _player.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> GetPlayerGold()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Items.OfType<Gold>()
            .Where(item =>
                item.Ownership.OwnerId == _player.Id
                && item.Ownership.OwnerType == OwnerType.Creature
            )
            .Select(item => item.Quantity)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<LearnFactFromCreatureObjective> SeedObjective(
        int baseWillingness = 0,
        int bribeWillingness = 0,
        int intimidationWillingness = 0,
        IReadOnlyCollection<Guid>? requiredSupportingQuestIds = null,
        IReadOnlyCollection<SupportingFactQuestWeight>? weightedSupportingQuestIds = null
    )
    {
        var quest = Builders.MakeQuest(_npc.Id, WorldId);
        var objective = Builders.MakeLearnFactFromCreatureObjective(
            quest.Id,
            _npc.Id,
            _fact.Id,
            WorldId,
            baseWillingness,
            bribeWillingness,
            intimidationWillingness,
            requiredSupportingQuestIds,
            weightedSupportingQuestIds
        );
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            Builders.MakeCreatureQuestObjective(_player.Id, objective.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return objective;
    }

    private async Task<List<FactDisclosureLockout>> LockoutsFor(
        Guid playerId,
        Guid npcId,
        Guid factId
    ) =>
        await _context
            .FactDisclosureLockouts.Where(lockout =>
                lockout.PlayerId == playerId && lockout.NpcId == npcId && lockout.FactId == factId
            )
            .ToListAsync(TestContext.Current.CancellationToken);

    private async Task<bool> HasLearnedTheFact()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext.CreatureKnowledge.AnyAsync(
            knowledge =>
                knowledge.KnowerId == _player.Id
                && knowledge.SubjectId == _fact.Id
                && knowledge.SubjectType == KnowledgeSubjectType.Fact,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task AskAboutFact_Discloses_WhenBaseWillingnessClearsTheThreshold()
    {
        // Arrange
        var objective = await SeedObjective(baseWillingness: 100);

        // Act
        var result = await _askHandler.Handle(
            new AskAboutFactCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(FactDisclosureOutcome.Disclosed, result.Outcome);
        Assert.Equal(_fact.Value, result.FactText);
        Assert.True(await HasLearnedTheFact());
        var progress = await _context.CreatureQuestObjectives.SingleAsync(
            p => p.ObjectiveId == objective.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(objective.RequiredAmount, progress.Amount);
    }

    [Fact]
    public async Task AskAboutFact_Fails_WhenScoreIsBelowTheThreshold()
    {
        // Arrange
        await SeedObjective(baseWillingness: 0);

        // Act
        var result = await _askHandler.Handle(
            new AskAboutFactCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(FactDisclosureOutcome.Failed, result.Outcome);
        Assert.False(await HasLearnedTheFact());
        Assert.Empty(await LockoutsFor(_player.Id, _npc.Id, _fact.Id));
        Assert.Empty(_serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents);
    }

    [Fact]
    public async Task AskAboutFact_NamesAnIncompleteRevealedWeightedQuest_WhenTheAttemptFails()
    {
        // Arrange
        var helpfulQuest = Builders.MakeQuest(_npc.Id, WorldId, name: "Lend a Hand");
        _context.Quests.Add(helpfulQuest);
        await SeedObjective(
            baseWillingness: 0,
            weightedSupportingQuestIds:
            [
                new SupportingFactQuestWeight { QuestId = helpfulQuest.Id, Weight = 10 },
            ]
        );

        // Act
        var result = await _askHandler.Handle(
            new AskAboutFactCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(FactDisclosureOutcome.Failed, result.Outcome);
        Assert.Equal(["Lend a Hand"], result.HelpfulQuestNames);
    }

    [Fact]
    public async Task AskAboutFact_OmitsACompletedOrHiddenWeightedQuest_FromHelpfulQuestNames()
    {
        // Arrange
        var completedQuest = Builders.MakeQuest(_npc.Id, WorldId, name: "Already Done");
        var hiddenQuest = Builders.MakeQuest(
            _npc.Id,
            WorldId,
            name: "Not Yet Discovered",
            revealedByFactId: Guid.NewGuid()
        );
        _context.Quests.AddRange(completedQuest, hiddenQuest);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = completedQuest.Id,
                Status = QuestStatus.Completed,
                WorldId = WorldId,
            }
        );
        await SeedObjective(
            baseWillingness: 0,
            weightedSupportingQuestIds:
            [
                new SupportingFactQuestWeight { QuestId = completedQuest.Id, Weight = 10 },
                new SupportingFactQuestWeight { QuestId = hiddenQuest.Id, Weight = 10 },
            ]
        );

        // Act
        var result = await _askHandler.Handle(
            new AskAboutFactCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(FactDisclosureOutcome.Failed, result.Outcome);
        Assert.Null(result.HelpfulQuestNames);
    }

    [Fact]
    public async Task AskAboutFact_RevealsAQuestGatedOnThisFact_EvenWhenTheAttemptFails()
    {
        // Arrange
        var hiddenQuest = Builders.MakeQuest(_npc.Id, WorldId, revealedByFactId: _fact.Id);
        _context.Quests.Add(hiddenQuest);
        await SeedObjective(baseWillingness: 0);

        // Act
        await _askHandler.Handle(
            new AskAboutFactCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var revealedQuest = await verifyContext.Quests.SingleAsync(
            q => q.Id == hiddenQuest.Id,
            TestContext.Current.CancellationToken
        );
        Assert.True(revealedQuest.IsRevealed);
        var gameEvents = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        Assert.Contains(
            gameEvents.EnqueuedEvents,
            gameEvent => gameEvent is QuestJournalUpdatedEvent
        );
    }

    [Fact]
    public async Task AskAboutFact_ReturnsBlockedWithTheMissingQuestName_WhenARequiredSupportingQuestIsIncomplete()
    {
        // Arrange
        var supportingQuest = Builders.MakeQuest(_npc.Id, WorldId, name: "Prove Yourself");
        _context.Quests.Add(supportingQuest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedObjective(baseWillingness: 100, requiredSupportingQuestIds: [supportingQuest.Id]);

        // Act
        var result = await _askHandler.Handle(
            new AskAboutFactCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(FactDisclosureOutcome.Blocked, result.Outcome);
        Assert.Equal(["Prove Yourself"], result.MissingRequiredQuestNames);
        Assert.False(await HasLearnedTheFact());
    }

    [Fact]
    public async Task OfferBribeForFact_Discloses_WhenTheBribeClearsTheThreshold()
    {
        // Arrange
        await SeedObjective(bribeWillingness: 100);

        // Act
        var result = await _bribeHandler.Handle(
            new OfferBribeForFactCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
                GoldOffered = 1000,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(FactDisclosureOutcome.Disclosed, result.Outcome);
        Assert.True(await HasLearnedTheFact());
        Assert.Equal(9000, await GetPlayerGold());
    }

    [Fact]
    public async Task OfferBribeForFact_ReturnsCannotAfford_WhenThePlayerLacksTheGold()
    {
        // Arrange
        var poorPlayer = Builders.MakeCreature(WorldId, level: 5);
        _context.Creatures.Add(poorPlayer);
        _context.Items.Add(Builders.MakeGold(WorldId, quantity: 100, ownerId: poorPlayer.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var quest = Builders.MakeQuest(_npc.Id, WorldId);
        var objective = Builders.MakeLearnFactFromCreatureObjective(
            quest.Id,
            _npc.Id,
            _fact.Id,
            WorldId,
            bribeWillingness: 100
        );
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = poorPlayer.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            Builders.MakeCreatureQuestObjective(poorPlayer.Id, objective.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _bribeHandler.Handle(
            new OfferBribeForFactCommand
            {
                WorldId = WorldId,
                PlayerId = poorPlayer.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
                GoldOffered = 1000,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(FactDisclosureOutcome.CannotAfford, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var gold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                item => item.Ownership.OwnerId == poorPlayer.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(100, gold.Quantity);
    }

    [Fact]
    public async Task OfferBribeForFact_LocksOutTheBribeApproach_WhenTheAttemptFails()
    {
        // Arrange
        await SeedObjective(bribeWillingness: 50);

        // Act
        var result = await _bribeHandler.Handle(
            new OfferBribeForFactCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
                GoldOffered = 10,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(FactDisclosureOutcome.Failed, result.Outcome);
        var lockout = Assert.Single(await LockoutsFor(_player.Id, _npc.Id, _fact.Id));
        Assert.Equal(FactDisclosureApproach.Bribe, lockout.Approach);
        Assert.Equal(_player.Id, lockout.PlayerId);
        Assert.Equal(_npc.Id, lockout.NpcId);
        Assert.Equal(_fact.Id, lockout.FactId);
        Assert.Equal(10_000, await GetPlayerGold());
    }

    [Fact]
    public async Task OfferBribeForFact_ReturnsLockedOut_WhenTheApproachAlreadyFailed()
    {
        // Arrange — a prior bribe attempt already failed and locked out the approach.
        await SeedObjective(bribeWillingness: 100);
        _context.FactDisclosureLockouts.Add(
            new FactDisclosureLockout
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
                Approach = FactDisclosureApproach.Bribe,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _bribeHandler.Handle(
            new OfferBribeForFactCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
                GoldOffered = 1000,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — locked out even though this offer would otherwise clear the threshold.
        Assert.Equal(FactDisclosureOutcome.LockedOut, result.Outcome);
        Assert.False(await HasLearnedTheFact());
    }

    [Fact]
    public async Task IntimidateForFact_ReturnsTooWeak_WhenThePlayerIsFarBelowTheNpcsLevel()
    {
        // Arrange
        var weakPlayer = Builders.MakeCreature(WorldId, level: 1);
        _context.Creatures.Add(weakPlayer);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var quest = Builders.MakeQuest(_npc.Id, WorldId);
        var objective = Builders.MakeLearnFactFromCreatureObjective(
            quest.Id,
            _npc.Id,
            _fact.Id,
            WorldId,
            intimidationWillingness: 100
        );
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = weakPlayer.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            Builders.MakeCreatureQuestObjective(weakPlayer.Id, objective.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _intimidateHandler.Handle(
            new IntimidateForFactCommand
            {
                WorldId = WorldId,
                PlayerId = weakPlayer.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — a hopeless attempt never locks out the approach.
        Assert.Equal(FactDisclosureOutcome.TooWeak, result.Outcome);
        Assert.Empty(await LockoutsFor(weakPlayer.Id, _npc.Id, _fact.Id));
    }

    [Fact]
    public async Task IntimidateForFact_Discloses_WhenTheLevelAdvantageClearsTheThreshold()
    {
        // Arrange
        var strongPlayer = Builders.MakeCreature(WorldId, level: 10);
        _context.Creatures.Add(strongPlayer);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var quest = Builders.MakeQuest(_npc.Id, WorldId);
        var objective = Builders.MakeLearnFactFromCreatureObjective(
            quest.Id,
            _npc.Id,
            _fact.Id,
            WorldId,
            intimidationWillingness: 100
        );
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = strongPlayer.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            Builders.MakeCreatureQuestObjective(strongPlayer.Id, objective.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _intimidateHandler.Handle(
            new IntimidateForFactCommand
            {
                WorldId = WorldId,
                PlayerId = strongPlayer.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(FactDisclosureOutcome.Disclosed, result.Outcome);
    }

    [Fact]
    public async Task Handle_ClearsEveryLockout_WhenAWeightedSupportingQuestCompletes()
    {
        // Arrange
        var supportingQuest = Builders.MakeQuest(_npc.Id, WorldId);
        _context.Quests.Add(supportingQuest);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = supportingQuest.Id,
                Status = QuestStatus.ReadyToComplete,
                WorldId = WorldId,
            }
        );
        await SeedObjective(
            bribeWillingness: 100,
            weightedSupportingQuestIds:
            [
                new SupportingFactQuestWeight { QuestId = supportingQuest.Id, Weight = 10 },
            ]
        );
        _context.FactDisclosureLockouts.AddRange(
            new FactDisclosureLockout
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
                Approach = FactDisclosureApproach.Bribe,
            },
            new FactDisclosureLockout
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
                Approach = FactDisclosureApproach.Intimidation,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _completeQuestHandler.Handle(
            new CompleteQuestCommand
            {
                PlayerId = _player.Id,
                QuestId = supportingQuest.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(await LockoutsFor(_player.Id, _npc.Id, _fact.Id));
    }
}
