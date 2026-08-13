using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Chat.Queries;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameSessions;

[Collection("Database")]
public sealed class GameSessionTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CreateGameSessionCommandHandler _createGameSession = null!;
    private GetGameSessionQueryHandler _getGameSession = null!;
    private GetOpenConversationsQueryHandler _getOpenConversations = null!;
    private GetPlaytimeQueryHandler _getPlaytime = null!;
    private AdvanceTimeCommandHandler _advanceTime = null!;
    private UpdateGameSessionCommandHandler _updateGameSession = null!;
    private GetChatMessagesQueryHandler _getChatMessages = null!;
    private AppendChatMessagesCommandHandler _appendChatMessages = null!;
    private ClearChatMessagesCommandHandler _clearChatMessages = null!;

    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _createGameSession = _serviceProvider.GetRequiredService<CreateGameSessionCommandHandler>();
        _getGameSession = _serviceProvider.GetRequiredService<GetGameSessionQueryHandler>();
        _getOpenConversations =
            _serviceProvider.GetRequiredService<GetOpenConversationsQueryHandler>();
        _getPlaytime = _serviceProvider.GetRequiredService<GetPlaytimeQueryHandler>();
        _updateGameSession = _serviceProvider.GetRequiredService<UpdateGameSessionCommandHandler>();
        _advanceTime = _serviceProvider.GetRequiredService<AdvanceTimeCommandHandler>();
        _getChatMessages = _serviceProvider.GetRequiredService<GetChatMessagesQueryHandler>();
        _appendChatMessages =
            _serviceProvider.GetRequiredService<AppendChatMessagesCommandHandler>();
        _clearChatMessages = _serviceProvider.GetRequiredService<ClearChatMessagesCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetGameSession_Throws_WhenSessionDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _getGameSession.Handle(
                new GetGameSessionQuery { SessionId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task CreateGameSession_Then_GetGameSession_ReturnsTheCreatedSnapshot()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                Playtime = TimeSpan.FromHours(3),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var snapshot = await _getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(WorldId, snapshot.WorldId);
        Assert.Equal(PlayerId, snapshot.PlayerId);
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
                WorldId = WorldId,
                PlayerId = PlayerId,
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
                WorldId = WorldId,
                PlayerId = PlayerId,
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
                WorldId = WorldId,
                PlayerId = PlayerId,
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
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
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
                WorldId = WorldId,
                PlayerId = PlayerId,
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
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
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
                WorldId = WorldId,
                PlayerId = PlayerId,
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
                WorldId = WorldId,
                PlayerId = PlayerId,
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
                WorldId = WorldId,
                PlayerId = PlayerId,
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
