using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs.Commands;

public sealed class SyncCreatureToCurrentJobCommandTests
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _restrainedLocationId = Guid.NewGuid();
    private readonly Guid _jobLocationId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly Creature _creature;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<SyncCreatureToCurrentJobCommand> _handler = null!;

    public SyncCreatureToCurrentJobCommandTests(DatabaseFixture database)
    {
        _database = database;
        _creature = Builders.MakeCreature(
            _worldId,
            locationId: _restrainedLocationId,
            state: CreatureState.Idle
        );
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<ICommandHandler<SyncCreatureToCurrentJobCommand>>();

        _context.Creatures.Add(_creature);
        // Playtime defaults to TimeSpan.Zero, which GameClock resolves to hour 8 at the world
        // epoch — matching MakeCreatureJob's default 8-17 Idle window below.
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, _creature.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RelocatesTheCreature_ToItsCurrentlyDueJob()
    {
        // Arrange
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                _creature.Id,
                action: CreatureJobAction.Work,
                locationId: _jobLocationId,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SyncCreatureToCurrentJobCommand { WorldId = _worldId, CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verification = _database.CreateContext();
        var creature = await verification.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_jobLocationId, creature.LocationId);
        Assert.Equal(CreatureState.Busy, creature.State);
    }

    [Fact]
    public async Task Handle_LeavesTheCreatureWhereItIs_WhenNoJobIsCurrentlyDue()
    {
        // Act
        await _handler.Handle(
            new SyncCreatureToCurrentJobCommand { WorldId = _worldId, CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verification = _database.CreateContext();
        var creature = await verification.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_restrainedLocationId, creature.LocationId);
        Assert.Equal(CreatureState.Idle, creature.State);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenTheCreatureDoesNotExist()
    {
        // Act & Assert
        await _handler.Handle(
            new SyncCreatureToCurrentJobCommand { WorldId = _worldId, CreatureId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );
    }
}
