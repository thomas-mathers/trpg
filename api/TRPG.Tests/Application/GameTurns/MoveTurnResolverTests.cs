using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class MoveTurnResolverTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private MoveTurnResolver _resolver = null!;
    private LocationConnector _connector = null!;
    private GameTurnSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _resolver = _serviceProvider.GetRequiredService<MoveTurnResolver>();

        var stateId = Guid.NewGuid();
        var origin = Builders.MakeLocation(_worldId, stateId);
        var destination = Builders.MakeLocation(_worldId, stateId);
        var player = Builders.MakeCreature(_worldId, locationId: origin.Id);
        _connector = Builders.MakeLocationConnector(
            origin.Id,
            destinationLocationId: destination.Id,
            destinationLabel: "Elsewhere",
            worldId: _worldId
        );
        var gameSession = Builders.MakeGameSession(_worldId, player.Id);
        _session = new GameTurnSession(gameSession.Id, _worldId, player.Id);

        _context.Worlds.Add(Builders.MakeWorld(_worldId));
        _context.States.Add(Builders.MakeState(Guid.NewGuid(), worldId: _worldId, id: stateId));
        _context.Locations.AddRange(origin, destination);
        _context.Creatures.Add(player);
        _context.LocationConnectors.Add(_connector);
        _context.GameSessions.Add(gameSession);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Resolve_RecordsTheArrivalInTheChatHistory_WhenTheMoveCompletes()
    {
        // Act
        await _resolver.Resolve(_session, _connector.Id, TestContext.Current.CancellationToken);

        // Assert
        var note = await _context
            .ChatMessages.Where(message => message.SessionId == _session.SessionId)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("user", note.Role);
        Assert.Contains("The player walked to", note.MessageJson);
    }

    [Fact]
    public async Task Resolve_LeavesTheChatHistoryAlone_WhenTheMoveIsRejected()
    {
        // Act
        await _resolver.Resolve(_session, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        var count = await _context.ChatMessages.CountAsync(
            message => message.SessionId == _session.SessionId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, count);
    }

    [Theory]
    [InlineData(EntryOutcome.NoEntrance, "no way in")]
    [InlineData(EntryOutcome.Locked, "The door is locked.")]
    [InlineData(EntryOutcome.DestinationNotFound, "nothing to enter")]
    [InlineData(EntryOutcome.ExitNotFound, "nothing to enter")]
    [InlineData(EntryOutcome.EncounterActive, "hostile encounter is already underway")]
    public void BuildRejectionMessage_ExplainsWhyTheMoveWasRefused(
        EntryOutcome outcome,
        string expected
    )
    {
        // Act
        var message = MoveTurnResolver.BuildRejectionMessage(outcome);

        // Assert
        Assert.Contains(expected, message);
    }
}
