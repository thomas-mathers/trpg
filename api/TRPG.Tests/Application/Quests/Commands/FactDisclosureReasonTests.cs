using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Knowledge.Commands;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Events;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Quests.Results;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Commands;

public sealed class FactDisclosureReasonTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private readonly Creature _player = Builders.MakeCreature(WorldId, level: 5);
    private readonly Creature _npc = Builders.MakeCreature(WorldId, level: 10);
    private readonly Fact _fact = new()
    {
        WorldId = WorldId,
        Subject = "the entrance",
        Value = "Use the sluice.",
    };
    private readonly Fact _reason = new()
    {
        WorldId = WorldId,
        Subject = "Mara's fear",
        Value = "They threatened my brother.",
    };
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _context.Creatures.AddRange(_player, _npc);
        _context.Facts.AddRange(_fact, _reason);
        _context.Items.Add(Builders.MakeGold(WorldId, quantity: 100, ownerId: _player.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<LearnFactFromCreatureObjective> SeedObjective(
        Guid? reasonId,
        int willingness = 0,
        IReadOnlyCollection<Guid>? requiredQuests = null
    )
    {
        var quest = Builders.MakeQuest(_npc.Id, WorldId);
        var objective = Builders.MakeLearnFactFromCreatureObjective(
            quest.Id,
            _npc.Id,
            _fact.Id,
            WorldId,
            baseWillingness: willingness,
            requiredSupportingQuestIds: requiredQuests,
            reasonFactId: reasonId
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

    private AskAboutFactCommand Ask =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            NpcId = _npc.Id,
            FactId = _fact.Id,
        };
    private IntimidateForFactCommand Threat =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            NpcId = _npc.Id,
            FactId = _fact.Id,
        };

    private OfferBribeForFactCommand Bribe(int gold) =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            NpcId = _npc.Id,
            FactId = _fact.Id,
            GoldOffered = gold,
        };

    [Fact]
    public async Task Ask_RecordsAttemptWithoutRevealingReason_WhenFirstAttemptFails()
    {
        // Arrange
        await SeedObjective(_reason.Id);

        // Act
        var result = await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(FactDisclosureOutcome.Failed, result.Outcome);
        Assert.Null(result.ReasonFact);
        await using var verify = db.CreateContext();
        Assert.Single(
            await verify
                .FactDisclosureAttempts.Where(x => x.PlayerId == _player.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
        Assert.False(
            await verify.CreatureKnowledge.AnyAsync(
                x => x.KnowerId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(_services.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents);
    }

    [Theory]
    [InlineData("ask")]
    [InlineData("bribe")]
    [InlineData("intimidate")]
    [InlineData("blocked")]
    public async Task Ask_RevealsOnlyReason_WhenAnotherQualifyingAttemptAlreadyFailed(
        string firstApproach
    )
    {
        // Arrange
        var supporting = Builders.MakeQuest(_npc.Id, WorldId);
        _context.Quests.Add(supporting);
        var objective = await SeedObjective(
            _reason.Id,
            requiredQuests: firstApproach == "blocked" ? [supporting.Id] : []
        );
        if (firstApproach == "bribe")
            await _services
                .GetRequiredService<
                    ICommandHandler<OfferBribeForFactCommand, FactDisclosureResult>
                >()
                .Handle(Bribe(10), TestContext.Current.CancellationToken);
        else if (firstApproach == "intimidate")
            await _services
                .GetRequiredService<
                    ICommandHandler<IntimidateForFactCommand, FactDisclosureResult>
                >()
                .Handle(Threat, TestContext.Current.CancellationToken);
        else
            await _services
                .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
                .Handle(Ask, TestContext.Current.CancellationToken);

        // Act
        var result = await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            firstApproach == "blocked"
                ? FactDisclosureOutcome.Blocked
                : FactDisclosureOutcome.Failed,
            result.Outcome
        );
        Assert.Equal(new DisclosedFact(_reason.Id, _reason.Value), result.ReasonFact);
        Assert.Null(result.FactText);
        await using var verify = db.CreateContext();
        var knowledge = await verify
            .CreatureKnowledge.Where(x => x.KnowerId == _player.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(_reason.Id, Assert.Single(knowledge).SubjectId);
        var progress = await verify.CreatureQuestObjectives.SingleAsync(
            x => x.ObjectiveId == objective.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, progress.Amount);
        Assert.Single(
            _services
                .GetRequiredService<TestGameClientEventSink>()
                .EnqueuedEvents.OfType<QuestJournalUpdatedEvent>()
        );
    }

    [Fact]
    public async Task Intimidate_RevealsReason_WhenSecondAttemptIsTooWeak()
    {
        // Arrange
        await SeedObjective(_reason.Id);
        await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);

        // Act
        var result = await _services
            .GetRequiredService<ICommandHandler<IntimidateForFactCommand, FactDisclosureResult>>()
            .Handle(Threat, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(FactDisclosureOutcome.TooWeak, result.Outcome);
        Assert.Equal(_reason.Id, result.ReasonFact?.FactId);
    }

    [Fact]
    public async Task Bribe_DoesNotRecordAttempt_WhenPlayerCannotAffordIt()
    {
        // Arrange
        await SeedObjective(_reason.Id);

        // Act
        var result = await _services
            .GetRequiredService<ICommandHandler<OfferBribeForFactCommand, FactDisclosureResult>>()
            .Handle(Bribe(101), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(FactDisclosureOutcome.CannotAfford, result.Outcome);
        await using var verify = db.CreateContext();
        Assert.False(
            await verify.FactDisclosureAttempts.AnyAsync(
                x => x.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Bribe_DoesNotRevealReason_WhenApproachIsLockedOut()
    {
        // Arrange
        await SeedObjective(_reason.Id);
        await _services
            .GetRequiredService<ICommandHandler<OfferBribeForFactCommand, FactDisclosureResult>>()
            .Handle(Bribe(10), TestContext.Current.CancellationToken);

        // Act
        var result = await _services
            .GetRequiredService<ICommandHandler<OfferBribeForFactCommand, FactDisclosureResult>>()
            .Handle(Bribe(20), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(FactDisclosureOutcome.LockedOut, result.Outcome);
        Assert.Null(result.ReasonFact);
        await using var verify = db.CreateContext();
        Assert.False(
            await verify.CreatureKnowledge.AnyAsync(
                x => x.KnowerId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Ask_DoesNotRevealReason_WhenPrimaryDisclosureSucceeds()
    {
        // Arrange
        await SeedObjective(_reason.Id, willingness: 100);
        _context.FactDisclosureAttempts.Add(
            new FactDisclosureAttempt
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                NpcId = _npc.Id,
                FactId = _fact.Id,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(FactDisclosureOutcome.Disclosed, result.Outcome);
        Assert.Equal(_fact.Value, result.FactText);
        Assert.Null(result.ReasonFact);
    }

    [Fact]
    public async Task Ask_ReturnsNoReason_WhenObjectiveHasNone()
    {
        // Arrange
        await SeedObjective(null);
        await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);

        // Act
        var result = await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result.ReasonFact);
    }

    [Fact]
    public async Task Ask_DoesNotRepeatDisclosure_WhenReasonIsAlreadyKnown()
    {
        // Arrange
        await SeedObjective(_reason.Id);
        await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);
        await _services
            .GetRequiredService<ICommandHandler<LearnFactCommand, bool>>()
            .Handle(
                new LearnFactCommand
                {
                    WorldId = WorldId,
                    KnowerId = _player.Id,
                    FactId = _reason.Id,
                },
                TestContext.Current.CancellationToken
            );
        var priorEventCount = _services
            .GetRequiredService<TestGameClientEventSink>()
            .EnqueuedEvents.Count;

        // Act
        var result = await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result.ReasonFact);
        Assert.Equal(
            priorEventCount,
            _services.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents.Count
        );
    }

    [Fact]
    public async Task Disclosure_OffersSupportingQuestWithoutAcceptingIt_WhenReasonIsLearned()
    {
        // Arrange
        var supporting = Builders.MakeQuest(_npc.Id, WorldId, requiredFactId: _reason.Id);
        _context.Quests.Add(supporting);
        await SeedObjective(_reason.Id, requiredQuests: [supporting.Id]);
        var interactions = _services.GetRequiredService<
            IQueryHandler<GetQuestInteractionsForGiverQuery, QuestInteractionsResult>
        >();
        var markers = _services.GetRequiredService<
            IQueryHandler<GetQuestMarkersForCreaturesQuery, QuestMarkersResult>
        >();
        var accept = _services.GetRequiredService<ICommandHandler<AcceptQuestCommand>>();
        var interactionsQuery = new GetQuestInteractionsForGiverQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            GiverId = _npc.Id,
        };
        var markerQuery = new GetQuestMarkersForCreaturesQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CreatureIds = [_npc.Id],
        };
        var acceptCommand = new AcceptQuestCommand
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            QuestId = supporting.Id,
        };
        var before = await interactions.Handle(
            interactionsQuery,
            TestContext.Current.CancellationToken
        );
        Assert.Empty(before.AvailableQuests);
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            accept.Handle(acceptCommand, TestContext.Current.CancellationToken)
        );

        // Act & Assert
        var first = await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);
        Assert.Null(first.ReasonFact);
        var afterFirst = await markers.Handle(markerQuery, TestContext.Current.CancellationToken);
        Assert.Empty(afterFirst.EntriesByCreatureId);
        var second = await _services
            .GetRequiredService<ICommandHandler<AskAboutFactCommand, FactDisclosureResult>>()
            .Handle(Ask, TestContext.Current.CancellationToken);
        Assert.Equal(_reason.Id, second.ReasonFact?.FactId);
        var afterSecond = await interactions.Handle(
            interactionsQuery,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(supporting.Id, Assert.Single(afterSecond.AvailableQuests).QuestId);
        var afterMarkers = await markers.Handle(markerQuery, TestContext.Current.CancellationToken);
        Assert.Equal(
            QuestMarker.Available,
            Assert.Single(afterMarkers.EntriesByCreatureId[_npc.Id]).Marker
        );
        Assert.False(
            await _context.CreatureQuests.AnyAsync(
                x => x.CreatureId == _player.Id && x.QuestId == supporting.Id,
                TestContext.Current.CancellationToken
            )
        );
        await accept.Handle(acceptCommand, TestContext.Current.CancellationToken);
        Assert.True(
            await _context.CreatureQuests.AnyAsync(
                x =>
                    x.CreatureId == _player.Id
                    && x.QuestId == supporting.Id
                    && x.Status == QuestStatus.Accepted,
                TestContext.Current.CancellationToken
            )
        );
    }
}
