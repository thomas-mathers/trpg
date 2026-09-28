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

    [Fact]
    public async Task Handle_ReleasesBothNpcs_WhenTwoConversationsAreOpen()
    {
        // Arrange
        var otherNpc = Builders.MakeCreature(WorldId, name: "Kellan", isEngaged: true);
        _context.Creatures.Add(otherNpc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _context
            .NpcConversationSessionStates.Where(s => s.SessionId == _session.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        s => s.OpenConversationCreatureIdsByName,
                        new Dictionary<string, Guid>
                        {
                            ["Mira"] = _npc.Id,
                            ["Kellan"] = otherNpc.Id,
                        }
                    ),
                TestContext.Current.CancellationToken
            );

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
            .Creatures.Where(creature =>
                creature.Id == _player.Id || creature.Id == _npc.Id || creature.Id == otherNpc.Id
            )
            .Select(creature => creature.IsEngaged)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, engagement.Length);
        Assert.All(engagement, Assert.False);
    }

    [Fact]
    public async Task Handle_RemovesOldHistoryAndTheForcedEndConversationTail_ButKeepsThisTurnsMessages()
    {
        // Arrange
        _context.ChatMessages.AddRange(
            Builders.MakeChatMessage(_session.Id, ordinal: 0),
            Builders.MakeChatMessage(_session.Id, ordinal: 1),
            Builders.MakeChatMessage(_session.Id, ordinal: 2),
            Builders.MakeChatMessage(_session.Id, ordinal: 3),
            Builders.MakeChatMessage(_session.Id, ordinal: 4)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CloseLingeringNpcConversationsCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = _player.Id,
                GameTime = GameClock.Epoch,
                CurrentTurnStart = 3,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var remainingOrdinals = await verifyContext
            .ChatMessages.Where(m => m.SessionId == _session.Id)
            .Select(m => m.Ordinal)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([0, 3, 4], remainingOrdinals.OrderBy(ordinal => ordinal));
    }
}
