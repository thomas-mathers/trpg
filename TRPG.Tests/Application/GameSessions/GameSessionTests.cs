using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Exceptions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;

namespace TRPG.Tests.Application.GameSessions;

[Collection("Database")]
public sealed class GameSessionTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private CreateGameSessionCommandHandler _createGameSession = null!;
    private GetGameSessionQueryHandler _getGameSession = null!;
    private GetOpenConversationsQueryHandler _getOpenConversations = null!;
    private GetPlaytimeQueryHandler _getPlaytime = null!;
    private AdvanceTimeCommandHandler _advanceTime = null!;
    private UpdateGameSessionCommandHandler _updateGameSession = null!;
    private GetChatMessagesQueryHandler _getChatMessages = null!;
    private AppendChatMessagesCommandHandler _appendChatMessages = null!;
    private ClearChatMessagesCommandHandler _clearChatMessages = null!;
    private Guid _worldId;
    private Guid _playerId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _createGameSession = new CreateGameSessionCommandHandler(_context);
        _getGameSession = new GetGameSessionQueryHandler(
            _context,
            NullLogger<GetGameSessionQueryHandler>.Instance
        );
        _getOpenConversations = new GetOpenConversationsQueryHandler(_context);
        _getPlaytime = new GetPlaytimeQueryHandler(
            _context,
            NullLogger<GetPlaytimeQueryHandler>.Instance
        );
        _updateGameSession = new UpdateGameSessionCommandHandler(
            _context,
            NullLogger<UpdateGameSessionCommandHandler>.Instance
        );
        _advanceTime = new AdvanceTimeCommandHandler(_getPlaytime, _updateGameSession);
        _getChatMessages = new GetChatMessagesQueryHandler(
            _context,
            NullLogger<GetChatMessagesQueryHandler>.Instance
        );
        _appendChatMessages = new AppendChatMessagesCommandHandler(
            _context,
            NullLogger<AppendChatMessagesCommandHandler>.Instance
        );
        _clearChatMessages = new ClearChatMessagesCommandHandler(
            _context,
            NullLogger<ClearChatMessagesCommandHandler>.Instance
        );
        _worldId = Guid.NewGuid();
        _playerId = Guid.NewGuid();
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task GetGameSession_Throws_WhenSessionDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<GameSessionNotFoundException>(() =>
            _getGameSession.Handle(
                new GetGameSessionQuery { SessionId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task CreateGameSession_Then_GetGameSession_ReturnsTheCreatedSnapshot()
    {
        // Act
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = _worldId,
                PlayerId = _playerId,
                Playtime = TimeSpan.FromHours(3),
            },
            TestContext.Current.CancellationToken
        );

        var snapshot = await _getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(_worldId, snapshot.WorldId);
        Assert.Equal(_playerId, snapshot.PlayerId);
        Assert.Equal(TimeSpan.FromHours(3), snapshot.Playtime);

        var messages = await _getChatMessages.Handle(
            new GetChatMessagesQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );
        Assert.Single(messages);
        Assert.Equal(ChatRole.System, messages[0].Role);
    }

    [Fact]
    public async Task UpdateGameSession_PersistsChangedFields()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = _worldId,
                PlayerId = _playerId,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = sessionId,
                Playtime = TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TimeSpan.FromHours(1), updated.Playtime);
    }

    [Fact]
    public async Task UpdateGameSession_DoesNothing_WhenNoFieldsAreSet()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = _worldId,
                PlayerId = _playerId,
                Playtime = TimeSpan.FromHours(2),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _updateGameSession.Handle(
            new UpdateGameSessionCommand { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TimeSpan.FromHours(2), updated.Playtime);
    }

    [Fact]
    public async Task UpdateGameSession_LeavesOmittedFieldsUnchanged()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = _worldId,
                PlayerId = _playerId,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );
        var npcId = Guid.NewGuid();
        await _updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = sessionId,
                OpenConversationCreatureIdsByName = new Dictionary<string, Guid>
                {
                    ["Some NPC"] = npcId,
                },
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = sessionId,
                Playtime = TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(npcId, updated.OpenConversationCreatureIdsByName["Some NPC"]);
        Assert.Equal(TimeSpan.FromHours(1), updated.Playtime);
    }

    [Fact]
    public async Task GetOpenConversations_Throws_WhenSessionDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<GameSessionNotFoundException>(() =>
            _getOpenConversations.Handle(
                new GetOpenConversationsQuery { SessionId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetOpenConversations_ReturnsWhatUpdateGameSessionPersisted()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = _worldId,
                PlayerId = _playerId,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );
        var npcId = Guid.NewGuid();

        // Act
        await _updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = sessionId,
                OpenConversationCreatureIdsByName = new Dictionary<string, Guid>
                {
                    ["Some NPC"] = npcId,
                },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var openConversations = await _getOpenConversations.Handle(
            new GetOpenConversationsQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(npcId, openConversations["Some NPC"]);
    }

    [Fact]
    public async Task GetPlaytime_Throws_WhenSessionDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<GameSessionNotFoundException>(() =>
            _getPlaytime.Handle(
                new GetPlaytimeQuery { SessionId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetPlaytime_ReturnsTheCurrentValue()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = _worldId,
                PlayerId = _playerId,
                Playtime = TimeSpan.FromHours(5),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var playtime = await _getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TimeSpan.FromHours(5), playtime);
    }

    [Fact]
    public async Task AdvanceTime_AdvancesAndPersistsAndReturnsTheNewValue()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = _worldId,
                PlayerId = _playerId,
                Playtime = TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var playtime = await _advanceTime.Handle(
            new AdvanceTimeCommand { SessionId = sessionId, Delta = TimeSpan.FromMinutes(30) },
            TestContext.Current.CancellationToken
        );

        // Assert
        var expected = TimeSpan.FromHours(1.5);
        Assert.Equal(expected, playtime);
        var persisted = await _getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expected, persisted);
    }

    [Fact]
    public async Task ClearChatMessages_KeepsTheSystemMessage_AndEverythingFromTheGivenOrdinalOnward()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = _worldId,
                PlayerId = _playerId,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );
        await _appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                SessionId = sessionId,
                Messages = [new ChatMessage(ChatRole.User, "turn one")],
            },
            TestContext.Current.CancellationToken
        );
        await _appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                SessionId = sessionId,
                Messages = [new ChatMessage(ChatRole.Assistant, "reply one")],
            },
            TestContext.Current.CancellationToken
        );
        var currentTurnStart = await _appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                SessionId = sessionId,
                Messages = [new ChatMessage(ChatRole.User, "turn two")],
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _clearChatMessages.Handle(
            new ClearChatMessagesCommand
            {
                SessionId = sessionId,
                KeepFromOrdinal = currentTurnStart,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var remaining = await _getChatMessages.Handle(
            new GetChatMessagesQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(2, remaining.Count);
        Assert.Equal(ChatRole.System, remaining[0].Role);
        Assert.Equal("turn two", remaining[1].Text);
    }
}
