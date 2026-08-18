using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.NpcConversations.Commands;

[Collection("Database")]
public sealed class CloseNpcConversationCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CloseNpcConversationCommandHandler _handler = null!;
    private GetNpcConversationSummaryQueryHandler _getSummary = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _getSummary = _serviceProvider.GetRequiredService<GetNpcConversationSummaryQueryHandler>();
        _handler = _serviceProvider.GetRequiredService<CloseNpcConversationCommandHandler>();

        _session = Builders.MakeGameSession(WorldId, PlayerId);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task OpenConversation(string npcName, Guid npcId)
    {
        _session.OpenConversationCreatureIdsByName[npcName] = npcId;
        await _context
            .GameSessions.Where(s => s.Id == _session.Id)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(
                        gs => gs.OpenConversationCreatureIdsByName,
                        _session.OpenConversationCreatureIdsByName
                    ),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task Handle_ReturnsNotOpen_WhenNoConversationIsOpenForThatNpc()
    {
        // Act
        var outcome = await _handler.Handle(
            new CloseNpcConversationCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcName = "Wraith",
                ConversationSummary = "They fought briefly.",
                Summary = "They fought briefly.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(CloseNpcConversationResult.NotOpen, outcome);
    }

    [Fact]
    public async Task Handle_ReturnsClosed_WhenAConversationIsOpenForThatNpc()
    {
        // Arrange
        var npcId = Guid.NewGuid();
        await OpenConversation("Wraith", npcId);

        // Act
        var outcome = await _handler.Handle(
            new CloseNpcConversationCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcName = "Wraith",
                ConversationSummary = "They fought briefly.",
                Summary = "They fought briefly.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(CloseNpcConversationResult.Closed, outcome);
    }

    [Fact]
    public async Task Handle_SavesTheSummary_ForTheOpenNpc()
    {
        // Arrange
        var npcId = Guid.NewGuid();
        await OpenConversation("Wraith", npcId);

        // Act
        await _handler.Handle(
            new CloseNpcConversationCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcName = "Wraith",
                ConversationSummary = "They fought briefly.",
                Summary = "They fought briefly.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = await _getSummary.Handle(
            new GetNpcConversationSummaryQuery { CreatureId = PlayerId, NpcId = npcId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal("They fought briefly.", summary);
    }

    [Fact]
    public async Task Handle_RemovesTheNpcFromOpenConversations()
    {
        // Arrange
        var npcId = Guid.NewGuid();
        await OpenConversation("Wraith", npcId);

        // Act
        await _handler.Handle(
            new CloseNpcConversationCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcName = "Wraith",
                ConversationSummary = "They fought briefly.",
                Summary = "They fought briefly.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _context
            .GameSessions.AsNoTracking()
            .SingleAsync(s => s.Id == _session.Id, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Wraith", updated.OpenConversationCreatureIdsByName.Keys);
    }
}
