using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Conversations.Commands;
using TRPG.Application.Conversations.Queries;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Conversations.Commands;

[Collection("Database")]
public sealed class CloseConversationCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private CloseConversationCommandHandler _handler = null!;
    private GetConversationSummaryQueryHandler _getSummary = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _getSummary = new GetConversationSummaryQueryHandler(_context);
        _handler = new CloseConversationCommandHandler(
            new GetGameSessionQueryHandler(
                _context,
                NullLogger<GetGameSessionQueryHandler>.Instance
            ),
            new SetConversationSummaryCommandHandler(_context),
            new UpdateGameSessionCommandHandler(
                _context,
                NullLogger<UpdateGameSessionCommandHandler>.Instance
            )
        );

        _session = Builders.MakeGameSession(WorldId, PlayerId);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

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
            new CloseConversationCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcName = "Wraith",
                Summary = "They fought briefly.",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(CloseConversationOutcome.NotOpen, outcome);
    }

    [Fact]
    public async Task Handle_ReturnsClosed_WhenAConversationIsOpenForThatNpc()
    {
        // Arrange
        var npcId = Guid.NewGuid();
        await OpenConversation("Wraith", npcId);

        // Act
        var outcome = await _handler.Handle(
            new CloseConversationCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcName = "Wraith",
                Summary = "They fought briefly.",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(CloseConversationOutcome.Closed, outcome);
    }

    [Fact]
    public async Task Handle_SavesTheSummary_ForTheOpenNpc()
    {
        // Arrange
        var npcId = Guid.NewGuid();
        await OpenConversation("Wraith", npcId);

        // Act
        await _handler.Handle(
            new CloseConversationCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcName = "Wraith",
                Summary = "They fought briefly.",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = await _getSummary.Handle(
            new GetConversationSummaryQuery { CreatureId = PlayerId, NpcId = npcId },
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
            new CloseConversationCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcName = "Wraith",
                Summary = "They fought briefly.",
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
