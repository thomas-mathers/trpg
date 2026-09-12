using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

public sealed class EvaluateJailbreakEncounterCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _guardStationLocationId = Guid.NewGuid();
    private readonly TestChanceRoller _chanceRoller = new() { Result = true };
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EvaluateJailbreakEncounterCommandHandler _handler = null!;
    private Creature _player = null!;
    private Faction _cityFaction = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EvaluateJailbreakEncounterCommandHandler>();

        _player = Builders.MakeCreature(WorldId, locationId: _guardStationLocationId);
        _cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true);

        _context.Locations.Add(
            Builders.MakeLocation(WorldId, Guid.NewGuid(), id: _guardStationLocationId)
        );
        _context.Creatures.Add(_player);
        _context.Factions.Add(_cityFaction);
        _context.GameSessions.Add(Builders.MakeGameSession(WorldId, _player.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ConfrontsTheEscapee_AndReportsTheEscapeToTheJailer()
    {
        // Arrange
        var jailer = await SeedJailer();
        var crime = SeedJailbreak();
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var encounter = await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(encounter);
        Assert.Equal(jailer.Id, encounter.GuardCreatureId);
        Assert.Equal(crime.Id, encounter.TriggeringCrimeId);

        await using var verifyContext = db.CreateContext();
        var witness = await verifyContext.CrimeWitnesses.SingleAsync(
            w => w.CrimeId == crime.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(jailer.Id, witness.CreatureId);
    }

    [Fact]
    public async Task Handle_LetsTheEscapeeWalkOut_WhenNoGuardIsThereToSeeThem()
    {
        // Arrange
        var crime = SeedJailbreak();
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var encounter = await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert — an escape nobody sees still costs nothing, the same as any unwitnessed crime.
        Assert.Null(encounter);

        await using var verifyContext = db.CreateContext();
        Assert.Empty(
            await verifyContext
                .CrimeWitnesses.Where(w => w.CrimeId == crime.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_IgnoresAnEscapeAlreadyAnsweredFor()
    {
        // Arrange
        await SeedJailer();
        var crime = SeedJailbreak();
        crime.Resolution = CrimeResolution.Reported;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var encounter = await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(encounter);
    }

    [Fact]
    public async Task Handle_LetsTheEscapeeSlipPast_WhenTheySneakByTheJailer()
    {
        // Arrange
        await SeedJailer();
        var crime = SeedJailbreak();
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await StartSneaking();
        _chanceRoller.Result = false;

        // Act
        var encounter = await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(encounter);

        await using var verifyContext = db.CreateContext();
        var stored = await verifyContext
            .Crimes.OfType<JailbreakCrime>()
            .SingleAsync(c => c.Id == crime.Id, TestContext.Current.CancellationToken);
        // Still open, so the next guard they walk past can catch them.
        Assert.Equal(CrimeResolution.Pending, stored.Resolution);
        Assert.Empty(
            await verifyContext
                .CrimeWitnesses.Where(w => w.CrimeId == crime.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_CatchesAnEscapeeWalkingOutInTheOpen_EvenWhenTheRollWouldFavourThem()
    {
        // Arrange
        await SeedJailer();
        SeedJailbreak();
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = false;

        // Act
        var encounter = await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert — walking out in plain sight is being seen, not a stealth check to win.
        Assert.NotNull(encounter);
    }

    private EvaluateJailbreakEncounterCommand MakeCommand() =>
        new() { WorldId = WorldId, PlayerId = _player.Id };

    private async Task StartSneaking() =>
        await _serviceProvider
            .GetRequiredService<ICommandHandler<SetSneakingCommand>>()
            .Handle(
                new SetSneakingCommand { CreatureId = _player.Id, IsSneaking = true },
                TestContext.Current.CancellationToken
            );

    private async Task<Creature> SeedJailer()
    {
        var jailer = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: _guardStationLocationId
        );
        _context.Creatures.Add(jailer);
        _context.FactionMembers.Add(
            Builders.MakeFactionMember(WorldId, _cityFaction.Id, jailer.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return jailer;
    }

    private JailbreakCrime SeedJailbreak()
    {
        var crime = new JailbreakCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = Guid.NewGuid(),
            BuildingId = Guid.NewGuid(),
            BuildingName = "The Iron Gate",
            OwnerFactionIds = [_cityFaction.Id],
        };
        _context.Crimes.Add(crime);
        return crime;
    }

    private sealed class TestChanceRoller : IChanceRoller
    {
        public bool Result { get; set; }

        public bool Roll(float chance) => Result;
    }
}
