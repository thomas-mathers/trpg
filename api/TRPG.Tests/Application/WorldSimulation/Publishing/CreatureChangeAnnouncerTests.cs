using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Events;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation.Publishing;

public sealed class CreatureChangeAnnouncerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant TwelveSecondsIn =
        GameClock.Epoch + TimeSpan.FromSeconds(12);

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _otherLocationId = Guid.NewGuid();

    private Creature _player = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CreatureChangeAnnouncer _announcer = null!;
    private PublishAmbientSceneCommandHandler _publishAmbientScene = null!;
    private TestGameClientEventSink _events = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<CreatureRegenOptions>>(
                new TestOptionsSnapshot<CreatureRegenOptions>(
                    new CreatureRegenOptions
                    {
                        HpRegenPercentPerTick = 0.2f,
                        ApRegenPercentPerTick = 0.25f,
                        MpRegenPercentPerTick = 0.25f,
                    }
                )
            )
            .BuildServiceProvider();
        _announcer = _serviceProvider.GetRequiredService<CreatureChangeAnnouncer>();
        _publishAmbientScene =
            _serviceProvider.GetRequiredService<PublishAmbientSceneCommandHandler>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        var country = Builders.MakeCountry(_worldId);
        var state = Builders.MakeState(country.Id);
        var world = Builders.MakeWorld(_worldId);
        _player = Builders.MakeCreature(_worldId, locationId: _locationId);
        world.PlayerId = _player.Id;
        _context.Worlds.Add(world);
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Locations.AddRange(
            Builders.MakeLocation(
                _worldId,
                state.Id,
                id: _locationId,
                kind: LocationKind.Wilderness
            ),
            Builders.MakeLocation(
                _worldId,
                state.Id,
                id: _otherLocationId,
                kind: LocationKind.Wilderness
            )
        );
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Announce_PublishesNothing_WhenNoSceneWasPublishedYet()
    {
        // Arrange
        var visitor = await SeedCreatureAt(_locationId);

        // Act
        await _announcer.Announce(
            _worldId,
            GameClock.Epoch,
            Changed(visitor.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Announce_PublishesOnlyAnArrival_WhenACreatureEntersThePlayersLocation()
    {
        // Arrange
        await PublishBaselineScene();
        var visitor = await SeedCreatureAt(_locationId);

        // Act
        await _announcer.Announce(
            _worldId,
            GameClock.Epoch,
            Changed(visitor.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var arrived = Assert.IsType<CreaturesArrivedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(visitor.Id, Assert.Single(arrived.Creatures).Id);
    }

    [Fact]
    public async Task Announce_PublishesOnlyADeparture_WhenACreatureLeavesThePlayersLocation()
    {
        // Arrange
        var resident = await SeedCreatureAt(_locationId);
        await PublishBaselineScene();
        await _context
            .Creatures.Where(creature => creature.Id == resident.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.LocationId, _otherLocationId),
                TestContext.Current.CancellationToken
            );

        // Act
        await _announcer.Announce(
            _worldId,
            GameClock.Epoch,
            Changed(resident.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var left = Assert.IsType<CreaturesLeftEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(resident.Id, Assert.Single(left.CreatureIds));
    }

    [Fact]
    public async Task Announce_PublishesAnUpdate_WhenANearbyCreaturesVitalsChanged()
    {
        // Arrange
        var resident = await SeedCreatureAt(_locationId);
        await PublishBaselineScene();
        await _context
            .Creatures.Where(creature => creature.Id == resident.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.CurrentHp, 1),
                TestContext.Current.CancellationToken
            );

        // Act
        await _announcer.Announce(
            _worldId,
            GameClock.Epoch,
            Changed(resident.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = Assert.IsType<CreaturesUpdatedEvent>(Assert.Single(_events.EnqueuedEvents));
        var creature = Assert.Single(updated.Creatures);
        Assert.Equal(resident.Id, creature.Id);
        Assert.Equal(1, creature.CurrentHp);
    }

    [Fact]
    public async Task Announce_PublishesAnUpdateOfThePlayer_WhenThePlayerEquipsGear()
    {
        // Arrange
        await PublishBaselineScene();
        await EquipChestArmor(_player);

        // Act
        await _announcer.Announce(
            _worldId,
            GameClock.Epoch,
            Changed(_player.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = Assert.IsType<CreaturesUpdatedEvent>(Assert.Single(_events.EnqueuedEvents));
        var creature = Assert.Single(updated.Creatures);
        Assert.Equal(_player.Id, creature.Id);
        Assert.Single(creature.Equipment);
    }

    [Fact]
    public async Task Announce_PublishesNothing_WhenThePlayerIsUnchanged()
    {
        // Arrange
        await PublishBaselineScene();

        // Act
        await _announcer.Announce(
            _worldId,
            GameClock.Epoch,
            Changed(_player.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Announce_PublishesNothing_WhenTheCreaturesAreUnchanged()
    {
        // Arrange
        var resident = await SeedCreatureAt(_locationId);
        await PublishBaselineScene();

        // Act
        await _announcer.Announce(
            _worldId,
            GameClock.Epoch,
            Changed(resident.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Announce_PublishesNothing_WhenTheChangedLocationIsNotThePlayers()
    {
        // Arrange
        await PublishBaselineScene();
        var visitor = await SeedCreatureAt(_otherLocationId);

        // Act
        await _announcer.Announce(
            _worldId,
            GameClock.Epoch,
            new Dictionary<Guid, IReadOnlySet<Guid>>
            {
                [_otherLocationId] = new HashSet<Guid> { visitor.Id },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Announce_CatchesUpRegenBeforeAnnouncing_WhenACreatureArrivesWithOwedRegen()
    {
        // Arrange
        await PublishBaselineScene();
        var visitor = await SeedCreatureAt(_locationId, currentHp: 1);

        // Act
        await _announcer.Announce(
            _worldId,
            TwelveSecondsIn,
            Changed(visitor.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var arrived = Assert.IsType<CreaturesArrivedEvent>(Assert.Single(_events.EnqueuedEvents));
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.Creatures.FindAsync(
            [visitor.Id],
            TestContext.Current.CancellationToken
        );
        Assert.True(persisted!.CurrentHp > 1);
        Assert.Equal(visitor.Id, Assert.Single(arrived.Creatures).Id);
    }

    private async Task EquipChestArmor(Creature creature)
    {
        var armor = Builders.MakeArmor(worldId: _worldId);
        armor.Quantity = 1;
        armor.Ownership.OwnerId = creature.Id;
        armor.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(armor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _serviceProvider
            .GetRequiredService<EquipInventoryItemCommandHandler>()
            .Handle(
                new EquipInventoryItemCommand
                {
                    CreatureId = creature.Id,
                    ItemId = armor.Id,
                    Slot = EquipmentSlot.Chest,
                },
                TestContext.Current.CancellationToken
            );
    }

    private IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> Changed(params Guid[] creatureIds) =>
        new Dictionary<Guid, IReadOnlySet<Guid>> { [_locationId] = new HashSet<Guid>(creatureIds) };

    private async Task<Creature> SeedCreatureAt(Guid locationId, int? currentHp = null)
    {
        var creature = Builders.MakeCreature(
            _worldId,
            locationId: locationId,
            currentHp: currentHp
        );
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    private async Task PublishBaselineScene()
    {
        await _publishAmbientScene.Handle(
            new PublishAmbientSceneCommand
            {
                WorldId = _worldId,
                PlayerId = _player.Id,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );
        _events.EnqueuedEvents.Clear();
    }
}
