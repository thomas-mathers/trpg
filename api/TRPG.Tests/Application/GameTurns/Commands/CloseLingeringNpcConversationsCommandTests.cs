using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Commands;

public sealed class CloseLingeringNpcConversationsCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Creature _player = Builders.MakeCreature(WorldId, isEngaged: true);
    private readonly Creature _npc = Builders.MakeCreature(WorldId, name: "Mira", isEngaged: true);
    private GameSession _session = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<CloseLingeringNpcConversationsCommand> _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _session = Builders.MakeGameSession(WorldId, _player.Id);
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        var turnContext = _serviceProvider.GetRequiredService<GameTurnContext>();
        turnContext.SessionId = _session.Id;
        turnContext.WorldId = WorldId;
        turnContext.PlayerId = _player.Id;
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<CloseLingeringNpcConversationsCommand>
        >();

        _context.Creatures.AddRange(_player, _npc);
        _context.GameSessions.Add(_session);
        _context.NpcConversationSessionStates.Add(
            new NpcConversationSessionState
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                OpenConversationCreatureIdsByName = new Dictionary<string, Guid>
                {
                    ["Mira"] = _npc.Id,
                },
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReleasesPlayerAndNpc_WhenModelDoesNotEndTheConversation()
    {
        // Act
        await _handler.Handle(
            new CloseLingeringNpcConversationsCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = _player.Id,
                GameTime = GameClock.Epoch,
                CurrentTurnStart = 0,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var engagement = await verifyContext
            .Creatures.Where(creature => creature.Id == _player.Id || creature.Id == _npc.Id)
            .Select(creature => creature.IsEngaged)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, engagement.Length);
        Assert.All(engagement, Assert.False);
    }
}
