using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Books.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class DungeonExpeditionFlowTests : IAsyncLifetime, IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly Location _location = Builders.MakeLocation(WorldId);
    private readonly Creature _player;
    private readonly Creature _survivor;
    private readonly Creature _companion;
    private readonly DungeonExpedition _expedition;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;

    public DungeonExpeditionFlowTests(DatabaseFixture database)
    {
        _database = database;
        _player = Builders.MakeCreature(WorldId, locationId: _location.Id);
        _survivor = Builders.MakeCreature(WorldId, locationId: _location.Id, name: "Survivor");
        _companion = Builders.MakeCreature(WorldId, state: CreatureState.Dead, name: "Companion");
        _expedition = Builders.MakeDungeonExpedition(_survivor, _companion);
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        var turnContext = _services.GetRequiredService<GameTurnContext>();
        turnContext.WorldId = WorldId;
        turnContext.PlayerId = _player.Id;
        _context.Locations.Add(_location);
        _context.Creatures.AddRange(_player, _survivor, _companion);
        _context.DungeonExpeditions.Add(_expedition);
        _context.BookWorks.Add(Builders.MakeExpeditionWork(_expedition));
        _context.BookPages.Add(Builders.MakeExpeditionPage(_expedition));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Cleanup_PreservesParticipants_WhenBothAreDead()
    {
        // Arrange
        _survivor.State = CreatureState.Dead;
        _companion.LocationId = _location.Id;
        var ordinaryCorpse = Builders.MakeCreature(
            WorldId,
            locationId: _location.Id,
            state: CreatureState.Dead
        );
        _context.Creatures.Add(ordinaryCorpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _services
            .GetRequiredService<ICommandHandler<CleanUpAbandonedCorpsesCommand>>()
            .Handle(
                new CleanUpAbandonedCorpsesCommand
                {
                    WorldId = WorldId,
                    PlayerId = _player.Id,
                    LocationId = _location.Id,
                },
                TestContext.Current.CancellationToken
            );

        // Assert
        await using var verification = _database.CreateContext();
        Assert.True(
            await verification.Creatures.AnyAsync(
                creature => creature.Id == _survivor.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verification.Creatures.AnyAsync(
                creature => creature.Id == _companion.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verification.Creatures.AnyAsync(
                creature => creature.Id == ordinaryCorpse.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GiveJournal_TeachesTheSurvivorTheAccount()
    {
        // Arrange
        await GiveJournalToPlayer();

        // Act
        await GiveJournalToSurvivor();

        // Assert
        var knowledge = await Knowledge();
        Assert.Equal(_expedition.Discovery, knowledge?.LearnedAccount);
    }

    [Fact]
    public async Task Handle_DoesNotTeachTheSurvivor_WhenJournalIsOnlyRead()
    {
        // Arrange
        await GiveJournalToPlayer();

        // Act
        await ReadJournal();

        // Assert
        var knowledge = await Knowledge();
        Assert.Null(knowledge?.LearnedAccount);
    }

    [Fact]
    public async Task AcceptQuest_MarksReadyToComplete_AssoonAsThePlayerRecoversTheJournal()
    {
        // Arrange
        var quest = Builders.MakeQuest(_survivor.Id, WorldId);
        var objective = Builders.MakeGiveItemObjective(
            quest.Id,
            _expedition.JournalItemId,
            _survivor.Id,
            WorldId
        );
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await AcceptQuest(quest.Id);

        // Act
        await AcquireJournal();

        // Assert
        await using var verification = _database.CreateContext();
        var creatureQuest = await verification.CreatureQuests.SingleAsync(
            creatureQuest =>
                creatureQuest.CreatureId == _player.Id && creatureQuest.QuestId == quest.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(QuestStatus.ReadyToComplete, creatureQuest.Status);
    }

    [Fact]
    public async Task CompleteQuest_GivesTheJournalToTheSurvivor_AndPaysGoldAndReputation()
    {
        // Arrange
        var quest = Builders.MakeQuest(_survivor.Id, WorldId);
        quest.ReputationRewards.Add(
            new QuestReputationReward
            {
                WorldId = WorldId,
                QuestId = quest.Id,
                TargetId = _survivor.Id,
                TargetType = ReputationTargetType.Creature,
                Score = 20,
            }
        );
        var objective = Builders.MakeGiveItemObjective(
            quest.Id,
            _expedition.JournalItemId,
            _survivor.Id,
            WorldId
        );
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await AcceptQuest(quest.Id);
        await AcquireJournal();

        // Act
        await _services
            .GetRequiredService<ICommandHandler<CompleteQuestCommand>>()
            .Handle(
                new CompleteQuestCommand
                {
                    PlayerId = _player.Id,
                    QuestId = quest.Id,
                    WorldId = WorldId,
                },
                TestContext.Current.CancellationToken
            );

        // Assert
        await using var afterCompletion = _database.CreateContext();
        Assert.Equal(
            QuestStatus.Completed,
            await afterCompletion
                .CreatureQuests.Where(creatureQuest =>
                    creatureQuest.CreatureId == _player.Id && creatureQuest.QuestId == quest.Id
                )
                .Select(creatureQuest => creatureQuest.Status)
                .SingleAsync(TestContext.Current.CancellationToken)
        );
        var journal = await afterCompletion.Items.SingleAsync(
            item => item.Id == _expedition.JournalItemId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_survivor.Id, journal.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, journal.Ownership.OwnerType);
        var knowledge = await Knowledge();
        Assert.Equal(_expedition.Discovery, knowledge?.LearnedAccount);
        var gold = await afterCompletion
            .Items.OfType<Gold>()
            .SingleAsync(
                item => item.Ownership.OwnerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(quest.GoldReward, gold.Quantity);
        Assert.True(
            await afterCompletion.Reputations.AnyAsync(
                reputation =>
                    reputation.CreatureId == _player.Id
                    && reputation.TargetId == _survivor.Id
                    && reputation.TargetType == ReputationTargetType.Creature
                    && reputation.Score == 20,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_RemovesExpedition_WhenWorldIsDropped()
    {
        // Act
        await _services
            .GetRequiredService<ICommandHandler<DropWorldCommand>>()
            .Handle(
                new DropWorldCommand { WorldId = WorldId },
                TestContext.Current.CancellationToken
            );

        // Assert
        await using var verification = _database.CreateContext();
        Assert.False(
            await verification.DungeonExpeditions.AnyAsync(
                expedition => expedition.WorldId == WorldId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verification.BookWorks.AnyAsync(
                work => work.Id == _expedition.JournalWorkId,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_UsesPersistedPremise_WhenComposingJournalContext()
    {
        // Arrange
        var building = Builders.MakeBuilding(
            worldId: WorldId,
            buildingType: BuildingType.Mine,
            id: _expedition.BuildingId
        );
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _services
            .GetRequiredService<
                ICommandHandler<EnsureExpeditionJournalContextCommand, ExpeditionJournalContext?>
            >()
            .Handle(
                new EnsureExpeditionJournalContextCommand(_expedition.JournalWorkId),
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.NotNull(result);
        await using var verification = _database.CreateContext();
        var premise = await verification
            .Buildings.Where(value => value.Id == building.Id)
            .Select(value => value.Premise)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(premise));
        Assert.Equal(premise, result.DungeonHistory);
        Assert.Equal(_expedition.FinalExperience, result.FinalExperience);
        Assert.Equal(_expedition.CompanionName, result.Author);
    }

    private async Task GiveJournalToPlayer()
    {
        var book = Builders.MakeBook(
            _expedition.JournalWorkId,
            id: _expedition.JournalItemId,
            worldId: WorldId
        );
        book.Ownership.OwnerId = _player.Id;
        book.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(book);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task GiveJournalToSurvivor() =>
        _services
            .GetRequiredService<ICommandHandler<TransferPlayerInventoryCommand>>()
            .Handle(
                new TransferPlayerInventoryCommand
                {
                    WorldId = WorldId,
                    To = new ItemOwnerReference(_survivor.Id, OwnerType.Creature),
                    Items = [new ItemSelection(_expedition.JournalItemId, 1)],
                    PlayerId = _player.Id,
                },
                TestContext.Current.CancellationToken
            );

    private Task AcceptQuest(Guid questId) =>
        _services
            .GetRequiredService<ICommandHandler<AcceptQuestCommand>>()
            .Handle(
                new AcceptQuestCommand
                {
                    PlayerId = _player.Id,
                    QuestId = questId,
                    WorldId = WorldId,
                },
                TestContext.Current.CancellationToken
            );

    // Simulates looting the journal from a corpse: the item starts owned by an arbitrary
    // non-player owner, and receiving it into the player's inventory is what fires
    // ItemAcquiredEvent to advance the quest objective.
    private async Task AcquireJournal()
    {
        var book = Builders.MakeBook(
            _expedition.JournalWorkId,
            id: _expedition.JournalItemId,
            worldId: WorldId
        );
        book.Ownership.OwnerId = _companion.Id;
        book.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(book);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _services
            .GetRequiredService<ICommandHandler<ReceivePlayerInventoryCommand>>()
            .Handle(
                new ReceivePlayerInventoryCommand
                {
                    WorldId = WorldId,
                    PlayerId = _player.Id,
                    From = new ItemOwnerReference(_companion.Id, OwnerType.Creature),
                    Items = [new ItemSelection(_expedition.JournalItemId, 1)],
                },
                TestContext.Current.CancellationToken
            );
    }

    private Task<ReadBookPageResult> ReadJournal() =>
        _services
            .GetRequiredService<ICommandHandler<ReadBookPageCommand, ReadBookPageResult>>()
            .Handle(
                new ReadBookPageCommand
                {
                    WorldId = WorldId,
                    ReaderId = _player.Id,
                    WorkId = _expedition.JournalWorkId,
                    PageNumber = 1,
                },
                TestContext.Current.CancellationToken
            );

    private Task<DungeonConversationKnowledge?> Knowledge() =>
        _services
            .GetRequiredService<
                IQueryHandler<GetDungeonConversationKnowledgeQuery, DungeonConversationKnowledge?>
            >()
            .Handle(
                new GetDungeonConversationKnowledgeQuery(
                    WorldId: WorldId,
                    PlayerId: _player.Id,
                    NpcId: _survivor.Id
                ),
                TestContext.Current.CancellationToken
            );
}
