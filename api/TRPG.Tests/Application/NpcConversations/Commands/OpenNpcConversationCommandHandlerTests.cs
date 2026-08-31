using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.NpcConversations.Commands;

[Collection("Database")]
public sealed class OpenNpcConversationCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly Guid _sessionId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private OpenNpcConversationCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<OpenNpcConversationCommandHandler>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOpened_WhenNoConversationIsOpenForThatNpc()
    {
        // Act
        var outcome = await _handler.Handle(
            new OpenNpcConversationCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = Guid.NewGuid(),
                NpcName = "Wraith",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(OpenNpcConversationResult.Opened, outcome);
    }

    [Fact]
    public async Task Handle_PersistsTheNpcIdUnderItsName()
    {
        // Arrange
        var npcId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new OpenNpcConversationCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = npcId,
                NpcName = "Wraith",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _context
            .NpcConversationSessionStates.AsNoTracking()
            .SingleAsync(s => s.SessionId == _sessionId, TestContext.Current.CancellationToken);
        Assert.Equal(npcId, updated.OpenConversationCreatureIdsByName["Wraith"]);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyOpen_WhenAConversationIsAlreadyOpenForThatNpc()
    {
        // Arrange
        await _handler.Handle(
            new OpenNpcConversationCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = Guid.NewGuid(),
                NpcName = "Wraith",
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var outcome = await _handler.Handle(
            new OpenNpcConversationCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                PlayerId = PlayerId,
                NpcId = Guid.NewGuid(),
                NpcName = "Wraith",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(OpenNpcConversationResult.AlreadyOpen, outcome);
    }
}
