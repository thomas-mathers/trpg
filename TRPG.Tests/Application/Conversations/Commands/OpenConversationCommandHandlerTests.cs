using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Conversations.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Conversations.Commands;

[Collection("Database")]
public sealed class OpenConversationCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private OpenConversationCommandHandler _handler = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<OpenConversationCommandHandler>();

        _session = Builders.MakeGameSession(WorldId, Guid.NewGuid());
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
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
            new OpenConversationCommand
            {
                SessionId = _session.Id,
                NpcId = Guid.NewGuid(),
                NpcName = "Wraith",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(OpenConversationOutcome.Opened, outcome);
    }

    [Fact]
    public async Task Handle_PersistsTheNpcIdUnderItsName()
    {
        // Arrange
        var npcId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new OpenConversationCommand
            {
                SessionId = _session.Id,
                NpcId = npcId,
                NpcName = "Wraith",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _context
            .GameSessions.AsNoTracking()
            .SingleAsync(s => s.Id == _session.Id, TestContext.Current.CancellationToken);
        Assert.Equal(npcId, updated.OpenConversationCreatureIdsByName["Wraith"]);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyOpen_WhenAConversationIsAlreadyOpenForThatNpc()
    {
        // Arrange
        await _handler.Handle(
            new OpenConversationCommand
            {
                SessionId = _session.Id,
                NpcId = Guid.NewGuid(),
                NpcName = "Wraith",
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var outcome = await _handler.Handle(
            new OpenConversationCommand
            {
                SessionId = _session.Id,
                NpcId = Guid.NewGuid(),
                NpcName = "Wraith",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(OpenConversationOutcome.AlreadyOpen, outcome);
    }
}
