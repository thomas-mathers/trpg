using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Knowledge.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

public sealed class EvaluateTrapEncounterCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EvaluateTrapEncounterCommandHandler _handler = null!;
    private readonly Location _location = Builders.MakeLocation(WorldId, Guid.NewGuid());
    private readonly Location _targetLocation = Builders.MakeLocation(WorldId, Guid.NewGuid());
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EvaluateTrapEncounterCommandHandler>();

        _player.LocationId = _location.Id;
        _context.Locations.AddRange(_location, _targetLocation);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Trap> SeedTrap(TrapKind kind = TrapKind.Mechanical)
    {
        var trap = Builders.MakeTrap(
            WorldId,
            locationId: _location.Id,
            targetId: _targetLocation.Id,
            trapKind: kind
        );
        _context.Props.Add(trap);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return trap;
    }

    private EvaluateTrapEncounterCommand MakeCommand() =>
        new() { WorldId = WorldId, PlayerId = _player.Id };

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoTrapIsAtTheLocation()
    {
        // Act
        var result = await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheTrapIsAlreadyResolved()
    {
        // Arrange
        var trap = await SeedTrap();
        trap.IsResolved = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_CreatesEncounterAndRecordsDiscovery()
    {
        // Arrange
        var trap = await SeedTrap(TrapKind.Water);

        // Act
        var result = await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(trap.Id, result.TrapId);
        Assert.Equal(TrapKind.Water, result.TrapKind);
        Assert.Equal(_targetLocation.Id, result.TargetLocationId);

        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext
            .Encounters.OfType<TrapEncounter>()
            .SingleAsync(e => e.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EncounterState.Active, persisted.State);

        var knowledgeQuery = _serviceProvider.GetRequiredService<
            IQueryHandler<GetKnownTrapIdsQuery, IReadOnlySet<Guid>>
        >();
        var known = await knowledgeQuery.Handle(
            new GetKnownTrapIdsQuery { CreatureId = _player.Id, TrapIds = [trap.Id] },
            TestContext.Current.CancellationToken
        );
        Assert.Contains(trap.Id, known);
    }
}
