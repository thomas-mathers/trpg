using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Chat.Queries;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Tests.Helpers;
using NpcConversationSessionState = TRPG.Domain.Models.NpcConversationSessionState;

namespace TRPG.Tests.Application.GameSessions;

public sealed class GameSessionTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CreateGameSessionCommandHandler _createGameSession = null!;
    private GetGameSessionQueryHandler _getGameSession = null!;
    private GetGameTimeQueryHandler _getGameTime = null!;
    private GetGameTimeByWorldIdQueryHandler _getGameTimeByWorldId = null!;
    private AdvanceTimeCommandHandler _advanceTime = null!;
    private GetChatMessagesQueryHandler _getChatMessages = null!;
    private AppendChatMessagesCommandHandler _appendChatMessages = null!;
    private ClearChatMessagesCommandHandler _clearChatMessages = null!;
    private DeleteGameSessionCommandHandler _deleteGameSession = null!;

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
        _getGameTime = _serviceProvider.GetRequiredService<GetGameTimeQueryHandler>();
        _getGameTimeByWorldId =
            _serviceProvider.GetRequiredService<GetGameTimeByWorldIdQueryHandler>();
        _advanceTime = _serviceProvider.GetRequiredService<AdvanceTimeCommandHandler>();
        _getChatMessages = _serviceProvider.GetRequiredService<GetChatMessagesQueryHandler>();
        _appendChatMessages =
            _serviceProvider.GetRequiredService<AppendChatMessagesCommandHandler>();
        _clearChatMessages = _serviceProvider.GetRequiredService<ClearChatMessagesCommandHandler>();
        _deleteGameSession = _serviceProvider.GetRequiredService<DeleteGameSessionCommandHandler>();
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
            new CreateGameSessionCommand { WorldId = WorldId, PlayerId = PlayerId },
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

        var messages = await _getChatMessages.Handle(
            new GetChatMessagesQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );
        Assert.Single(messages);
        Assert.Equal(ChatRole.System, messages[0].Role);
    }

    [Fact]
    public async Task GetGameTime_Throws_WhenSessionDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _getGameTime.Handle(
                new GetGameTimeQuery { SessionId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetGameTime_ReturnsTheCurrentValue()
    {
        // Arrange
        var expected = GameClock.Epoch + TimeSpan.FromHours(5);
        var world = Builders.MakeWorld();
        world.GameTime = expected;
        _context.Worlds.Add(world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand { WorldId = world.Id, PlayerId = PlayerId },
            TestContext.Current.CancellationToken
        );

        // Act
        var gameTime = await _getGameTime.Handle(
            new GetGameTimeQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(expected, gameTime);
    }

    [Fact]
    public async Task GetGameTimeByWorldId_Throws_WhenWorldDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _getGameTimeByWorldId.Handle(
                new GetGameTimeByWorldIdQuery { WorldId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetGameTimeByWorldId_ReturnsTheCurrentValue()
    {
        // Arrange
        var expected = GameClock.Epoch + TimeSpan.FromHours(5);
        var world = Builders.MakeWorld();
        world.GameTime = expected;
        _context.Worlds.Add(world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var gameTime = await _getGameTimeByWorldId.Handle(
            new GetGameTimeByWorldIdQuery { WorldId = world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(expected, gameTime);
    }

    [Fact]
    public async Task AdvanceTime_AdvancesAndPersistsAndReturnsTheNewValue()
    {
        // Arrange
        var world = Builders.MakeWorld();
        world.GameTime = GameClock.Epoch + TimeSpan.FromHours(1);
        _context.Worlds.Add(world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand { WorldId = world.Id, PlayerId = PlayerId },
            TestContext.Current.CancellationToken
        );

        // Act
        var gameTime = await _advanceTime.Handle(
            new AdvanceTimeCommand { WorldId = world.Id, Delta = TimeSpan.FromMinutes(30) },
            TestContext.Current.CancellationToken
        );

        // Assert
        var expected = GameClock.Epoch + TimeSpan.FromHours(1.5);
        Assert.Equal(expected, gameTime);
        var persisted = await _getGameTime.Handle(
            new GetGameTimeQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expected, persisted);
    }

    [Fact]
    public async Task ClearChatMessages_KeepsTheSystemMessage_AndEverythingFromTheGivenOrdinalOnward()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand { WorldId = WorldId, PlayerId = PlayerId },
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

    [Fact]
    public async Task Handle_DeletesChatAndNpcConversationSessionState_WhenGameSessionIsDeleted()
    {
        // Arrange
        var sessionId = await _createGameSession.Handle(
            new CreateGameSessionCommand { WorldId = WorldId, PlayerId = PlayerId },
            TestContext.Current.CancellationToken
        );
        await _appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                SessionId = sessionId,
                Messages = [new ChatMessage(ChatRole.User, "hello")],
            },
            TestContext.Current.CancellationToken
        );
        _context.NpcConversationSessionStates.Add(
            new NpcConversationSessionState { SessionId = sessionId, WorldId = WorldId }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _deleteGameSession.Handle(
            new DeleteGameSessionCommand { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.GameSessions.AnyAsync(
                session => session.Id == sessionId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verifyContext.ChatMessages.AnyAsync(
                message => message.SessionId == sessionId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await verifyContext.NpcConversationSessionStates.AnyAsync(
                state => state.SessionId == sessionId,
                TestContext.Current.CancellationToken
            )
        );
    }
}
