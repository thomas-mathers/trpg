using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.LocationSimulation;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class ResetAlertedCreaturesCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResetAlertedCreaturesCommandHandler _handler = null!;
    private Location _location = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResetAlertedCreaturesCommandHandler>();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId);
        _location = Builders.MakeLocation(WorldId, _stateId);
        _session = Builders.MakeGameSession(WorldId, Guid.NewGuid());
        _context.States.Add(state);
        _context.Locations.Add(_location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ClearsAlert_ForAlertedCreaturesAtTheLocation()
    {
        // Arrange
        var alertedMonster = Builders.MakeCreature(
            WorldId,
            locationId: _location.Id,
            state: CreatureState.Alerted
        );
        _context.Creatures.Add(alertedMonster);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ResetAlertedCreaturesCommand { WorldId = WorldId, LocationId = _location.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedMonster = await verifyContext.Creatures.FindAsync(
            [alertedMonster.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Idle, updatedMonster!.State);
    }

    [Fact]
    public async Task Handle_LeavesCreaturesIdle_WhenNoCreaturesAreAlerted()
    {
        // Arrange
        var idleMonster = Builders.MakeCreature(
            WorldId,
            locationId: _location.Id,
            state: CreatureState.Idle
        );
        _context.Creatures.Add(idleMonster);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ResetAlertedCreaturesCommand { WorldId = WorldId, LocationId = _location.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var unchangedMonster = await verifyContext.Creatures.FindAsync(
            [idleMonster.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Idle, unchangedMonster!.State);
    }
}
