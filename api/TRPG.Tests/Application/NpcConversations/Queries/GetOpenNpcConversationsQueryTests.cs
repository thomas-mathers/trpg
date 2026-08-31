using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.NpcConversations.Queries;

[Collection("Database")]
public sealed class GetOpenNpcConversationsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetOpenNpcConversationsQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetOpenNpcConversationsQueryHandler>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyDictionary_WhenNoConversationsHaveEverBeenOpened()
    {
        // Act
        var result = await _handler.Handle(
            new GetOpenNpcConversationsQuery { SessionId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsThePersistedOpenConversations()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var npcId = Guid.NewGuid();
        _context.NpcConversationSessionStates.Add(
            new NpcConversationSessionState
            {
                SessionId = sessionId,
                WorldId = WorldId,
                OpenConversationCreatureIdsByName = new Dictionary<string, Guid>
                {
                    ["Wraith"] = npcId,
                },
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetOpenNpcConversationsQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(npcId, result["Wraith"]);
    }
}
