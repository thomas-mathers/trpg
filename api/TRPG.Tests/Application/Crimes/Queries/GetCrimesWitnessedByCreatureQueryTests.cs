using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Queries;

public sealed class GetCrimesWitnessedByCreatureQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetCrimesWitnessedByCreatureQueryHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);
    private readonly Creature _witness = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetCrimesWitnessedByCreatureQueryHandler>();

        _context.Creatures.AddRange(_player, _witness);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsEveryCrimeKindTheWitnessSaw_OrderedByMostRecentFirst()
    {
        // Arrange
        var kill = Builders.MakeKillCrime(
            WorldId,
            _player.Id,
            LocationId,
            victimName: "Victim",
            occurredAt: DateTime.UtcNow.AddMinutes(-30)
        );
        var assault = Builders.MakeAssaultCrime(
            WorldId,
            _player.Id,
            LocationId,
            victimName: "Bystander",
            occurredAt: DateTime.UtcNow.AddMinutes(-25)
        );
        var theft = Builders.MakeTheftCrime(
            WorldId,
            _player.Id,
            LocationId,
            ownerName: "Mara",
            occurredAt: DateTime.UtcNow.AddMinutes(-20)
        );
        var breakIn = Builders.MakeLockpickingCrime(
            WorldId,
            _player.Id,
            LocationId,
            buildingName: "The Sundry Store",
            occurredAt: DateTime.UtcNow.AddMinutes(-10)
        );
        var trespass = Builders.MakeTrespassingCrime(
            WorldId,
            _player.Id,
            LocationId,
            buildingName: "The Gilded Manor",
            occurredAt: DateTime.UtcNow.AddMinutes(-5)
        );
        _context.Crimes.AddRange(kill, assault, theft, breakIn, trespass);
        _context.CrimeWitnesses.AddRange(
            Builders.MakeCrimeWitness(kill.Id, _witness.Id, WorldId),
            Builders.MakeCrimeWitness(assault.Id, _witness.Id, WorldId),
            Builders.MakeCrimeWitness(theft.Id, _witness.Id, WorldId),
            Builders.MakeCrimeWitness(breakIn.Id, _witness.Id, WorldId),
            Builders.MakeCrimeWitness(trespass.Id, _witness.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCrimesWitnessedByCreatureQuery
            {
                WorldId = WorldId,
                WitnessCreatureId = _witness.Id,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — OccurredAt round-trips through Postgres with lower precision than the in-memory
        // DateTime, so compare it separately from the fields that survive the round trip exactly.
        Assert.Equal(
            [
                (WitnessedCrimeKind.Trespassing, "The Gilded Manor", (TheftCrimeOutcome?)null),
                (WitnessedCrimeKind.Lockpicking, "The Sundry Store", null),
                (WitnessedCrimeKind.Theft, "Mara", TheftCrimeOutcome.Taken),
                (WitnessedCrimeKind.Assault, "Bystander", null),
                (WitnessedCrimeKind.Kill, "Victim", null),
            ],
            result.Select(crime => (crime.Kind, crime.SubjectName, crime.Outcome))
        );
        Assert.True(result[0].OccurredAt > result[1].OccurredAt);
        Assert.True(result[1].OccurredAt > result[2].OccurredAt);
        Assert.True(result[2].OccurredAt > result[3].OccurredAt);
        Assert.True(result[3].OccurredAt > result[4].OccurredAt);
    }

    [Fact]
    public async Task Handle_ExcludesACrime_WhenTheWitnessEndedUpDead()
    {
        // Arrange
        var kill = Builders.MakeKillCrime(WorldId, _player.Id, LocationId);
        _context.Crimes.Add(kill);
        _context.CrimeWitnesses.Add(
            Builders.MakeCrimeWitness(
                kill.Id,
                _witness.Id,
                WorldId,
                resolution: CrimeWitnessResolution.Dead
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCrimesWitnessedByCreatureQuery
            {
                WorldId = WorldId,
                WitnessCreatureId = _witness.Id,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
