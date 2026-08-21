using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Reputations.Events;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Events;

[Collection("Database")]
public sealed class CreatureKilledCrimeWitnessEventHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CreatureKilledCrimeWitnessEventHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<CreatureKilledCrimeWitnessEventHandler>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ResolvesTheftCrimeAndEnqueuesNotification_WhenFinalWitnessDies()
    {
        // Arrange
        var witness = Builders.MakeCreature(WorldId, locationId: LocationId);
        var crime = MakeTheftCrime();
        _context.Creatures.Add(witness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(MakeWitness(crime.Id, witness.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CreatureKilledEvent(_player.Id, WorldId, witness.Id, CreatureType.Human),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        var persistedWitness = await verifyContext.CrimeWitnesses.SingleAsync(
            item => item.CrimeId == crime.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(CrimeResolution.Unreported, persistedCrime!.Resolution);
        Assert.Equal(CrimeWitnessResolution.Dead, persistedWitness.Resolution);
        Assert.Contains(
            _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents,
            gameEvent => gameEvent == new CrimeWitnessesRemovedEvent(CrimeKind.Theft)
        );
    }

    [Fact]
    public async Task Handle_ResolvesKillCrimeAndEnqueuesNotification_WhenFinalWitnessDies()
    {
        // Arrange
        var victim = Builders.MakeCreature(WorldId, locationId: LocationId);
        var witness = Builders.MakeCreature(WorldId, locationId: LocationId);
        var crime = new KillCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            VictimId = victim.Id,
            VictimName = victim.Name,
        };
        _context.Creatures.AddRange(victim, witness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(MakeWitness(crime.Id, witness.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CreatureKilledEvent(_player.Id, WorldId, witness.Id, CreatureType.Human),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(CrimeResolution.Unreported, persistedCrime!.Resolution);
        Assert.Contains(
            _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents,
            gameEvent => gameEvent == new CrimeWitnessesRemovedEvent(CrimeKind.Killing)
        );
    }

    [Fact]
    public async Task Handle_LeavesCrimePendingAndDoesNotEnqueueNotification_WhenAnotherWitnessSurvives()
    {
        // Arrange
        var deadWitness = Builders.MakeCreature(WorldId, locationId: LocationId);
        var livingWitness = Builders.MakeCreature(WorldId, locationId: LocationId);
        var crime = MakeTheftCrime();
        _context.Creatures.AddRange(deadWitness, livingWitness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.AddRange(
            MakeWitness(crime.Id, deadWitness.Id),
            MakeWitness(crime.Id, livingWitness.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CreatureKilledEvent(_player.Id, WorldId, deadWitness.Id, CreatureType.Human),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        var witnessResolutions = await verifyContext
            .CrimeWitnesses.Where(witness => witness.CrimeId == crime.Id)
            .ToDictionaryAsync(
                witness => witness.CreatureId,
                witness => witness.Resolution,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(CrimeResolution.Pending, persistedCrime!.Resolution);
        Assert.Equal(CrimeWitnessResolution.Dead, witnessResolutions[deadWitness.Id]);
        Assert.Equal(CrimeWitnessResolution.Pending, witnessResolutions[livingWitness.Id]);
        Assert.DoesNotContain(
            _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents,
            gameEvent => gameEvent is CrimeWitnessesRemovedEvent
        );
    }

    private TheftCrime MakeTheftCrime() =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            OwnerCreatureId = Guid.NewGuid(),
            OwnerName = "Mara",
            SourceOwnerId = Guid.NewGuid(),
            SourceOwnerType = OwnerType.Container,
        };

    private static CrimeWitness MakeWitness(Guid crimeId, Guid creatureId) =>
        new()
        {
            WorldId = WorldId,
            CrimeId = crimeId,
            CreatureId = creatureId,
        };
}
