using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class RecordAssaultCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RecordAssaultCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<RecordAssaultCommandHandler>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RecordsTheCrimeWithTheVictimAmongTheWitnesses_WhenTheVictimIsACitizen()
    {
        // Arrange
        var victim = await SeedVictim();

        // Act
        await _handler.Handle(
            new RecordAssaultCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                VictimId = victim.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — the victim witnesses their own assault, so a lone traveller still reports it
        await using var verifyContext = db.CreateContext();
        var crime = await verifyContext
            .Crimes.OfType<AssaultCrime>()
            .SingleAsync(c => c.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(victim.Id, crime.VictimId);
        Assert.NotEmpty(crime.VictimFactionIds);

        var witnessIds = await verifyContext
            .CrimeWitnesses.Where(w => w.CrimeId == crime.Id)
            .Select(w => w.CreatureId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Contains(victim.Id, witnessIds);
    }

    [Fact]
    public async Task Handle_RecordsNothing_WhenTheVictimHasNoFaction()
    {
        // Arrange
        var victim = Builders.MakeCreature(WorldId, locationId: LocationId);
        _context.Creatures.Add(victim);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new RecordAssaultCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                VictimId = victim.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var hasCrime = await verifyContext
            .Crimes.OfType<AssaultCrime>()
            .AnyAsync(c => c.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.False(hasCrime);
    }

    [Fact]
    public async Task Handle_RecordsNothing_WhenTheVictimIsNotHumanoid()
    {
        // Arrange
        var victim = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            creatureType: CreatureType.Beast
        );
        var faction = Builders.MakeFaction(WorldId);
        _context.Creatures.Add(victim);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, victim.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new RecordAssaultCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                VictimId = victim.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var hasCrime = await verifyContext
            .Crimes.OfType<AssaultCrime>()
            .AnyAsync(c => c.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.False(hasCrime);
    }

    private async Task<Creature> SeedVictim()
    {
        var victim = Builders.MakeCreature(WorldId, locationId: LocationId);
        var faction = Builders.MakeFaction(WorldId);

        _context.Creatures.Add(victim);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, victim.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return victim;
    }
}
