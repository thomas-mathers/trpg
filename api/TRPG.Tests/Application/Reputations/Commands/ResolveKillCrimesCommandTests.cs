using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Reputations.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Commands;

[Collection("Database")]
public sealed class ResolveKillCrimesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveKillCrimesCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsMonitor<ReputationOptions>>(
                new TestOptionsMonitor<ReputationOptions>(
                    new ReputationOptions { KillReputationPenalty = -31 }
                )
            )
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveKillCrimesCommandHandler>();
        _player = Builders.MakeCreature(WorldId, locationId: LocationId);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReportsMovedWitnessAndMarksDeadWitnessDead()
    {
        var faction = Builders.MakeFaction(WorldId);
        var victim = Builders.MakeCreature(WorldId, locationId: LocationId);
        var movedWitness = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var deadWitness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = new KillCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            VictimId = victim.Id,
            VictimName = victim.Name,
        };
        _context.Factions.Add(faction);
        _context.Creatures.AddRange(victim, movedWitness, deadWitness);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, victim.Id));
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.AddRange(
            new CrimeWitness
            {
                WorldId = WorldId,
                CrimeId = crime.Id,
                CreatureId = movedWitness.Id,
            },
            new CrimeWitness
            {
                WorldId = WorldId,
                CrimeId = crime.Id,
                CreatureId = deadWitness.Id,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _handler.Handle(
            new ResolveKillCrimesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = LocationId,
            },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var witnesses = await verifyContext
            .CrimeWitnesses.Where(witness => witness.CrimeId == crime.Id)
            .ToDictionaryAsync(
                witness => witness.CreatureId,
                witness => witness.Resolution,
                TestContext.Current.CancellationToken
            );
        var log = await verifyContext.ReputationLogEntries.SingleAsync(
            entry => entry.CreatureId == _player.Id && entry.TargetId == faction.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(CrimeWitnessResolution.Reported, witnesses[movedWitness.Id]);
        Assert.Equal(CrimeWitnessResolution.Dead, witnesses[deadWitness.Id]);
        Assert.Equal(-31, log.DeltaScore);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
