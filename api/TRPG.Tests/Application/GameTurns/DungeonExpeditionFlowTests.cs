using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Books.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.NpcConversations.Tools;
using TRPG.Tests.Helpers;
using TRPG.Tools;

namespace TRPG.Tests.Application.GameTurns;

[Collection("Database")]
public sealed class DungeonExpeditionFlowTests : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly Location _location = Builders.MakeLocation(WorldId);
    private readonly Creature _player;
    private readonly Creature _survivor;
    private readonly Creature _companion;
    private readonly DungeonExpedition _expedition;
    private readonly GameSession _session;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;

    public DungeonExpeditionFlowTests(DatabaseFixture database)
    {
        _database = database;
        _player = Builders.MakeCreature(WorldId, locationId: _location.Id);
        _survivor = Builders.MakeCreature(WorldId, locationId: _location.Id, name: "Survivor");
        _companion = Builders.MakeCreature(WorldId, state: CreatureState.Dead, name: "Companion");
        _expedition = Builders.MakeDungeonExpedition(_survivor, _companion);
        _session = Builders.MakeGameSession(WorldId, _player.Id);
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        var turnContext = _services.GetRequiredService<GameTurnContext>();
        turnContext.WorldId = WorldId;
        turnContext.PlayerId = _player.Id;
        turnContext.SessionId = _session.Id;
        _context.Locations.Add(_location);
        _context.Creatures.AddRange(_player, _survivor, _companion);
        _context.GameSessions.Add(_session);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Share_PersistsOnlyAfterExplicitDisclosure_WhenDiscoveryOrderVaries(
        bool readFirst
    )
    {
        // Arrange
        if (readFirst)
            await ReadJournal();
        await OpenConversation();
        if (!readFirst)
            await ReadJournal();
        var before = await Knowledge();
        Assert.NotNull(before?.PlayerCanShare);
        Assert.Null(before.LearnedAccount);

        // Act
        var result = await _services
            .GetRequiredService<
                ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?>
            >()
            .Handle(
                new ShareExpeditionDiscoveryCommand(
                    WorldId: WorldId,
                    SessionId: _session.Id,
                    PlayerId: _player.Id,
                    ExpeditionId: _expedition.Id
                ),
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.Equal(_expedition.Discovery, result?.LearnedAccount);
        await using var verification = _database.CreateContext();
        Assert.Single(
            await verification
                .CreatureKnowledge.Where(knowledge => knowledge.KnowerId == _survivor.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
        Assert.False(
            await verification.CreatureKnowledge.AnyAsync(
                knowledge =>
                    knowledge.KnowerId == _player.Id
                    && knowledge.SubjectType == KnowledgeSubjectType.Room,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Share_DoesNotDuplicateKnowledge_WhenRepeated()
    {
        // Arrange
        await ReadJournal();
        await OpenConversation();
        await _services
            .GetRequiredService<
                ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?>
            >()
            .Handle(
                new ShareExpeditionDiscoveryCommand(
                    WorldId: WorldId,
                    SessionId: _session.Id,
                    PlayerId: _player.Id,
                    ExpeditionId: _expedition.Id
                ),
                TestContext.Current.CancellationToken
            );

        // Act
        await _services
            .GetRequiredService<
                ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?>
            >()
            .Handle(
                new ShareExpeditionDiscoveryCommand(
                    WorldId: WorldId,
                    SessionId: _session.Id,
                    PlayerId: _player.Id,
                    ExpeditionId: _expedition.Id
                ),
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.Equal(
            1,
            await _context.CreatureKnowledge.CountAsync(
                knowledge => knowledge.KnowerId == _survivor.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Share_RejectsDisclosure_WhenJournalWasNotRead()
    {
        // Arrange
        await OpenConversation();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _services
                .GetRequiredService<
                    ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?>
                >()
                .Handle(
                    new ShareExpeditionDiscoveryCommand(
                        WorldId: WorldId,
                        SessionId: _session.Id,
                        PlayerId: _player.Id,
                        ExpeditionId: _expedition.Id
                    ),
                    TestContext.Current.CancellationToken
                )
        );
    }

    [Fact]
    public async Task Share_RejectsDisclosure_WhenConversationIsNotOpen()
    {
        // Arrange
        await ReadJournal();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _services
                .GetRequiredService<
                    ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?>
                >()
                .Handle(
                    new ShareExpeditionDiscoveryCommand(
                        WorldId: WorldId,
                        SessionId: _session.Id,
                        PlayerId: _player.Id,
                        ExpeditionId: _expedition.Id
                    ),
                    TestContext.Current.CancellationToken
                )
        );
    }

    [Theory]
    [InlineData(CreatureState.Dead)]
    [InlineData(CreatureState.Sleeping)]
    public async Task Share_RejectsDisclosure_WhenSurvivorCannotConverse(CreatureState state)
    {
        // Arrange
        await ReadJournal();
        await OpenConversation();
        _survivor.State = state;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _services
                .GetRequiredService<
                    ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?>
                >()
                .Handle(
                    new ShareExpeditionDiscoveryCommand(
                        WorldId: WorldId,
                        SessionId: _session.Id,
                        PlayerId: _player.Id,
                        ExpeditionId: _expedition.Id
                    ),
                    TestContext.Current.CancellationToken
                )
        );
    }

    [Fact]
    public async Task Share_RejectsDisclosure_WhenSurvivorHasMovedAway()
    {
        // Arrange
        await ReadJournal();
        await OpenConversation();
        _survivor.LocationId = Guid.NewGuid();
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _services
                .GetRequiredService<
                    ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?>
                >()
                .Handle(
                    new ShareExpeditionDiscoveryCommand(
                        WorldId: WorldId,
                        SessionId: _session.Id,
                        PlayerId: _player.Id,
                        ExpeditionId: _expedition.Id
                    ),
                    TestContext.Current.CancellationToken
                )
        );
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invoke_ReturnsValidatedOutcome_WhenPlayerSharesJournal(bool hasRead)
    {
        // Arrange
        await OpenConversation();
        if (hasRead)
            await ReadJournal();
        var tool = _services.GetRequiredService<ShareExpeditionDiscoveryTool>();
        var invoke = (Func<Guid, CancellationToken, Task<object?>>)tool.Invoke;

        // Act
        var result = await invoke(_expedition.Id, TestContext.Current.CancellationToken);

        // Assert
        if (hasRead)
            Assert.NotNull(Assert.IsType<DungeonConversationKnowledge>(result).LearnedAccount);
        else
            Assert.IsType<ToolError>(result);
    }

    [Fact]
    public async Task Handle_RefreshesShareableKnowledge_WhenJournalIsReadDuringConversation()
    {
        // Arrange
        await OpenConversation();
        await ReadJournal();

        // Act
        var result = await _services
            .GetRequiredService<
                IQueryHandler<
                    GetOpenDungeonConversationKnowledgeQuery,
                    IReadOnlyList<DungeonConversationKnowledge>
                >
            >()
            .Handle(
                new GetOpenDungeonConversationKnowledgeQuery(
                    WorldId: WorldId,
                    SessionId: _session.Id,
                    PlayerId: _player.Id
                ),
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.NotNull(Assert.Single(result).PlayerCanShare);
        Assert.Null(result[0].LearnedAccount);
    }

    [Fact]
    public async Task Handle_DoesNotTeachDiscovery_WhenPageIsPrefetched()
    {
        // Act
        await _services
            .GetRequiredService<ICommandHandler<EnsureBookPageCommand, string>>()
            .Handle(
                new EnsureBookPageCommand { WorkId = _expedition.JournalWorkId, PageNumber = 1 },
                TestContext.Current.CancellationToken
            );

        // Assert
        var knowledge = await Knowledge();
        Assert.Null(knowledge?.PlayerCanShare);
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

    [Fact]
    public async Task Share_RejectsDisclosure_WhenSessionBelongsToAnotherPlayer()
    {
        // Arrange
        await ReadJournal();
        await OpenConversation();
        var otherSession = Builders.MakeGameSession(WorldId, _survivor.Id);
        _context.GameSessions.Add(otherSession);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _services
                .GetRequiredService<
                    ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?>
                >()
                .Handle(
                    new ShareExpeditionDiscoveryCommand(
                        WorldId: WorldId,
                        SessionId: otherSession.Id,
                        PlayerId: _player.Id,
                        ExpeditionId: _expedition.Id
                    ),
                    TestContext.Current.CancellationToken
                )
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

    private Task<OpenNpcConversationResult> OpenConversation() =>
        _services
            .GetRequiredService<
                ICommandHandler<OpenNpcConversationCommand, OpenNpcConversationResult>
            >()
            .Handle(
                new OpenNpcConversationCommand
                {
                    WorldId = WorldId,
                    SessionId = _session.Id,
                    PlayerId = _player.Id,
                    NpcId = _survivor.Id,
                    NpcName = _survivor.Name,
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
