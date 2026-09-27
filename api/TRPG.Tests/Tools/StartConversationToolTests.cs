using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.NpcConversations.Tools;
using TRPG.Tests.Helpers;
using TRPG.Tools;

namespace TRPG.Tests.Tools;

public sealed class StartConversationToolTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private StartConversationTool _tool = null!;
    private EndConversationTool _endTool = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _player = Builders.MakeCreature(_worldId, locationId: _locationId);
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _tool = _serviceProvider.GetRequiredService<StartConversationTool>();
        _endTool = _serviceProvider.GetRequiredService<EndConversationTool>();
        var turnContext = _serviceProvider.GetRequiredService<GameTurnContext>();
        turnContext.PlayerId = _player.Id;
        turnContext.WorldId = _worldId;

        var session = Builders.MakeGameSession(_worldId, _player.Id);
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
        var npc = Builders.MakeCreature(_worldId, locationId: _locationId, name: "Mara");
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
        var npc = Builders.MakeCreature(_worldId, locationId: _locationId, name: "Mara");
        _context.Creatures.Add(npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke(npc.Name, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsNotType<ToolError>(result);
        _context.ChangeTracker.Clear();
        var participants = await _context
            .Creatures.Where(creature => creature.Id == _player.Id || creature.Id == npc.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.All(participants, creature => Assert.True(creature.IsEngaged));
    }

    [Fact]
    public async Task EndConversation_ReleasesBothParticipants()
    {
        var npc = Builders.MakeCreature(_worldId, locationId: _locationId, name: "Mara");
        _context.Creatures.Add(npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var start = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;
        await start(npc.Name, TestContext.Current.CancellationToken);

        var endTask =
            (Task<object?>)
                _endTool.Invoke.DynamicInvoke(
                    npc.Name,
                    "They exchanged greetings.",
                    "They have met once.",
                    null,
                    null,
                    null,
                    null,
                    TestContext.Current.CancellationToken
                )!;
        var result = await endTask;

        Assert.IsNotType<ToolError>(result);
        _context.ChangeTracker.Clear();
        var participants = await _context
            .Creatures.Where(creature => creature.Id == _player.Id || creature.Id == npc.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.All(participants, creature => Assert.False(creature.IsEngaged));
    }
}
