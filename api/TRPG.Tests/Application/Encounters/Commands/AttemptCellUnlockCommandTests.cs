using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

public sealed class AttemptCellUnlockCommandTests : IAsyncLifetime, IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Location _location;
    private readonly Creature _player;
    private readonly Cell _cell;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<AttemptCellUnlockCommand, AttemptCellUnlockResult> _handler = null!;

    public AttemptCellUnlockCommandTests(DatabaseFixture database)
    {
        Database = database;
        _location = Builders.MakeLocation(_worldId);
        _player = Builders.MakeCreature(_worldId, locationId: _location.Id, name: "Player");
        _cell = new Cell
        {
            WorldId = _worldId,
            LocationId = _location.Id,
            Name = "Cell",
            Description = "A test cell",
            CreatureId = Guid.NewGuid(),
            IsLocked = true,
            LockLevel = 20,
        };
    }

    private DatabaseFixture Database { get; }

    public async ValueTask InitializeAsync()
    {
        _context = Database.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<
            ICommandHandler<AttemptCellUnlockCommand, AttemptCellUnlockResult>
        >();

        _context.Locations.Add(_location);
        _context.Creatures.Add(_player);
        _context.Props.Add(_cell);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_Opens_WhenThePlayerHoldsTheMatchingKey()
    {
        // Arrange
        var key = Builders.MakeKey(_worldId, quantity: 1, ownerId: _player.Id);
        _context.Items.Add(key);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _context
            .Props.OfType<Cell>()
            .Where(cell => cell.Id == _cell.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(cell => cell.KeyItemId, key.Id),
                TestContext.Current.CancellationToken
            );

        // Act
        var result = await _handler.Handle(
            new AttemptCellUnlockCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                CellId = _cell.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(CellUnlockOutcome.Opened, result.Outcome);
        Assert.Null(result.Encounter);
        await using var verification = Database.CreateContext();
        var cell = await verification
            .Props.OfType<Cell>()
            .SingleAsync(cell => cell.Id == _cell.Id, TestContext.Current.CancellationToken);
        Assert.False(cell.IsLocked);
        Assert.Null(cell.CreatureId);
    }

    [Fact]
    public async Task Handle_ReturnsNothingToUnlock_WhenTheCellIsAlreadyUnlocked()
    {
        // Arrange
        await _context
            .Props.OfType<Cell>()
            .Where(cell => cell.Id == _cell.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(cell => cell.IsLocked, false),
                TestContext.Current.CancellationToken
            );

        // Act
        var result = await _handler.Handle(
            new AttemptCellUnlockCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                CellId = _cell.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(CellUnlockOutcome.NothingToUnlock, result.Outcome);
    }

    [Fact]
    public async Task Handle_StartsAHostileEncounter_WhenALivingGuardNoticesANonSneakingPlayer()
    {
        // Arrange — RollDetection always reports detected when the player isn't sneaking, so a
        // living guard here makes the outcome deterministic regardless of the lockpick roll.
        var guard = Builders.MakeCreature(_worldId, locationId: _location.Id, name: "Guard");
        var faction = Builders.MakeFaction(worldId: _worldId);
        var group = Builders.MakeEncounterGroup(_worldId, _location.Id, faction.Id);
        var member = Builders.MakeEncounterGroupMember(_worldId, group.Id, guard.Id);
        _context.Creatures.Add(guard);
        _context.Factions.Add(faction);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(member);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new AttemptCellUnlockCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                CellId = _cell.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(CellUnlockOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Encounter);
        await using var verification = Database.CreateContext();
        var cell = await verification
            .Props.OfType<Cell>()
            .SingleAsync(cell => cell.Id == _cell.Id, TestContext.Current.CancellationToken);
        Assert.True(cell.IsLocked);
    }
}
