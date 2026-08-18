using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.NpcConversations.Commands;

[Collection("Database")]
public sealed class RecordNpcConversationCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RecordNpcConversationCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<RecordNpcConversationCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesHistoryAndMessage_WhenNoHistoryExists()
    {
        // Arrange
        var npcId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new RecordNpcConversationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = npcId,
                Summary = "They have met once.",
                ConversationSummary = "They discussed the weather.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var history = await _context.NpcConversationHistories.SingleAsync(
            item => item.CreatureId == PlayerId && item.NpcId == npcId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("They have met once.", history.Summary);

        var message = await _context.NpcConversations.SingleAsync(
            item => item.NpcConversationHistoryId == history.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("They discussed the weather.", message.Summary);
    }

    [Fact]
    public async Task Handle_UpdatesSummary_AndAppendsMessage_WhenHistoryAlreadyExists()
    {
        // Arrange
        var npcId = Guid.NewGuid();
        var history = new NpcConversationHistory
        {
            WorldId = WorldId,
            CreatureId = PlayerId,
            NpcId = npcId,
            Summary = "They have met once.",
        };
        _context.NpcConversationHistories.Add(history);
        _context.NpcConversations.Add(
            new NpcConversation
            {
                WorldId = WorldId,
                NpcConversationHistoryId = history.Id,
                Summary = "They discussed the weather.",
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new RecordNpcConversationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = npcId,
                Summary = "They have met twice now.",
                ConversationSummary = "They discussed the harvest.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updatedHistory = await _context.NpcConversationHistories.SingleAsync(
            item => item.Id == history.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("They have met twice now.", updatedHistory.Summary);

        var messages = await _context
            .NpcConversations.Where(item => item.NpcConversationHistoryId == history.Id)
            .OrderBy(item => item.CreatedAt)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, messages.Length);
        Assert.Equal("They discussed the weather.", messages[0].Summary);
        Assert.Equal("They discussed the harvest.", messages[1].Summary);
    }

    [Fact]
    public async Task Handle_AppendsDurableFactsAndOpenThreads_WhenAdded()
    {
        // Arrange
        var npcId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new RecordNpcConversationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = npcId,
                Summary = "First meeting.",
                ConversationSummary = "They spoke for the first time.",
                DurableFactsAdded = ["Player's name is Elena"],
                DurableFactsRemoved = [],
                OpenThreadsAdded = ["Promised to bring back a coin"],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var history = await _context.NpcConversationHistories.SingleAsync(
            item => item.CreatureId == PlayerId && item.NpcId == npcId,
            TestContext.Current.CancellationToken
        );
        var fact = Assert.Single(history.DurableFacts);
        Assert.Equal("Player's name is Elena", fact.Text);
        Assert.False(fact.IsRetracted);

        var thread = Assert.Single(history.OpenThreads);
        Assert.Equal("Promised to bring back a coin", thread.Text);
        Assert.False(thread.IsResolved);
    }

    [Fact]
    public async Task Handle_RetractsFactAndResolvesThread_ByOrdinal_WithoutDeletingThem()
    {
        // Arrange
        var npcId = Guid.NewGuid();
        var history = new NpcConversationHistory
        {
            WorldId = WorldId,
            CreatureId = PlayerId,
            NpcId = npcId,
            Summary = "They have met once.",
            DurableFacts = [new NpcDurableFact("Player's name is Elena")],
            OpenThreads = [new NpcOpenThread("Promised to bring back a coin")],
        };
        _context.NpcConversationHistories.Add(history);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new RecordNpcConversationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = npcId,
                Summary = "Elena brought the coin.",
                ConversationSummary = "She followed through on her promise.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [1],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [1],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updatedHistory = await _context.NpcConversationHistories.SingleAsync(
            item => item.Id == history.Id,
            TestContext.Current.CancellationToken
        );
        var fact = Assert.Single(updatedHistory.DurableFacts);
        Assert.True(fact.IsRetracted);
        var thread = Assert.Single(updatedHistory.OpenThreads);
        Assert.True(thread.IsResolved);
    }

    [Fact]
    public async Task Handle_IgnoresOutOfRangeRemovalOrdinals()
    {
        // Arrange
        var npcId = Guid.NewGuid();
        var history = new NpcConversationHistory
        {
            WorldId = WorldId,
            CreatureId = PlayerId,
            NpcId = npcId,
            Summary = "They have met once.",
            DurableFacts = [new NpcDurableFact("Player's name is Elena")],
        };
        _context.NpcConversationHistories.Add(history);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new RecordNpcConversationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = npcId,
                Summary = "They have met once.",
                ConversationSummary = "Nothing new.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [0, 5, -1],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updatedHistory = await _context.NpcConversationHistories.SingleAsync(
            item => item.Id == history.Id,
            TestContext.Current.CancellationToken
        );
        var fact = Assert.Single(updatedHistory.DurableFacts);
        Assert.False(fact.IsRetracted);
    }

    [Fact]
    public async Task Handle_ResolvesOrdinalsAgainstOnlyCurrentlyActiveItems()
    {
        // Arrange
        var npcId = Guid.NewGuid();
        var history = new NpcConversationHistory
        {
            WorldId = WorldId,
            CreatureId = PlayerId,
            NpcId = npcId,
            Summary = "They have met once.",
            DurableFacts =
            [
                new NpcDurableFact("Player's name is Elena"),
                new NpcDurableFact("Player is from Millhaven"),
            ],
        };
        _context.NpcConversationHistories.Add(history);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Retract the first active fact (ordinal 1 of 2).
        await _handler.Handle(
            new RecordNpcConversationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = npcId,
                Summary = "They have met once.",
                ConversationSummary = "The player corrected their name.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [1],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Act — with one fact retracted, the remaining fact is now active ordinal 1.
        await _handler.Handle(
            new RecordNpcConversationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = npcId,
                Summary = "They have met once.",
                ConversationSummary = "The player left town.",
                DurableFactsAdded = [],
                DurableFactsRemoved = [1],
                OpenThreadsAdded = [],
                OpenThreadsRemoved = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updatedHistory = await _context.NpcConversationHistories.SingleAsync(
            item => item.Id == history.Id,
            TestContext.Current.CancellationToken
        );
        Assert.All(updatedHistory.DurableFacts, fact => Assert.True(fact.IsRetracted));
    }
}
