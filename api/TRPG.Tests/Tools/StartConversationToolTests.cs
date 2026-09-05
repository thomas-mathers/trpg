using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.NpcConversations.Tools;
using TRPG.Tests.Helpers;
using TRPG.Tools;

namespace TRPG.Tests.Tools;

[Collection("Database")]
public sealed class StartConversationToolTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private StartConversationTool _tool = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _tool = _serviceProvider.GetRequiredService<StartConversationTool>();
        var turnContext = _serviceProvider.GetRequiredService<GameTurnContext>();
        turnContext.PlayerId = _player.Id;
        turnContext.WorldId = WorldId;

        var session = Builders.MakeGameSession(WorldId, _player.Id);
        turnContext.SessionId = session.Id;
        _context.Creatures.Add(_player);
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Invoke_OpensTheConversation_WhenThePlayerIsSneaking()
    {
        // Arrange
        var npc = Builders.MakeCreature(WorldId, locationId: LocationId, name: "Mara");
        _context.Creatures.Add(npc);
        _player.IsSneaking = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke(npc.Name, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsNotType<ToolError>(result);
    }

    [Fact]
    public async Task Invoke_OpensTheConversation_WhenThePlayerIsNotSneaking()
    {
        // Arrange
        var npc = Builders.MakeCreature(WorldId, locationId: LocationId, name: "Mara");
        _context.Creatures.Add(npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke(npc.Name, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsNotType<ToolError>(result);
    }
}
