using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Buildings;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class ResolvePlayerRespawnCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolvePlayerRespawnCommandHandler _handler = null!;
    private Guid _sanctuaryLocationId;
    private readonly Creature _player = Builders.MakeCreature(
        WorldId,
        state: CreatureState.Dead,
        currentHp: 0
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolvePlayerRespawnCommandHandler>();

        _sanctuaryLocationId = await SeedCityWithTempleAndDeathLocation(_player.LocationId);

        var item = Builders.MakeWeapon(WorldId, quantity: 1);
        item.Ownership.OwnerId = _player.Id;
        _context.Creatures.Add(_player);
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DropsCorpseAndRevivesThePlayer_WhenNoClericIsPresent()
    {
        // Arrange
        var deathLocationId = _player.LocationId;
        var command = new ResolvePlayerRespawnCommand { WorldId = WorldId, PlayerId = _player.Id };

        // Act
        var fact = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var corpse = await verifyContext.Creatures.SingleAsync(
            creature => creature.PlayerCorpseOwnerId == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(deathLocationId, corpse.LocationId);
        Assert.Equal(CreatureState.Dead, corpse.State);

        var corpseItemCount = await verifyContext.Items.CountAsync(
            item => item.Ownership.OwnerId == corpse.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, corpseItemCount);

        var revivedPlayer = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_sanctuaryLocationId, revivedPlayer.LocationId);
        Assert.Equal(CreatureState.Idle, revivedPlayer.State);
        Assert.Equal(revivedPlayer.MaximumHp, revivedPlayer.CurrentHp);

        Assert.Equal(1, fact.ItemsLeftOnCorpse);
        Assert.False(fact.IsClericPresent);
        Assert.Null(fact.ClericName);
    }

    [Fact]
    public async Task Handle_ReportsTheCleric_WhenOneIsPresentAtTheSanctuary()
    {
        // Arrange
        var cleric = Builders.MakeCreature(
            WorldId,
            profession: Profession.Cleric,
            locationId: _sanctuaryLocationId,
            state: CreatureState.Idle
        );
        _context.Creatures.Add(cleric);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var command = new ResolvePlayerRespawnCommand { WorldId = WorldId, PlayerId = _player.Id };

        // Act
        var fact = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(fact.IsClericPresent);
        Assert.Equal(cleric.Name, fact.ClericName);
    }

    private async Task<Guid> SeedCityWithTempleAndDeathLocation(Guid deathLocationId)
    {
        var countryId = Guid.NewGuid();
        var state = Builders.MakeState(countryId, WorldId);
        var city = Builders.MakeCity(state.Id, countryId, worldId: WorldId);
        var cityEntranceDistrict = Builders.MakeDistrict(
            city.Id,
            DistrictType.CityEntrance,
            WorldId,
            locationId: deathLocationId
        );
        var deathLocation = Builders.MakeLocation(
            WorldId,
            stateId: state.Id,
            cityId: city.Id,
            districtId: cityEntranceDistrict.Id,
            id: deathLocationId
        );

        var holySiteDistrict = Builders.MakeDistrict(city.Id, DistrictType.HolySite, WorldId);
        var templeExteriorLocation = Builders.MakeLocation(
            WorldId,
            stateId: state.Id,
            cityId: city.Id,
            districtId: holySiteDistrict.Id,
            id: holySiteDistrict.LocationId
        );
        var temple = Builders.MakeBuilding(
            exteriorLocationId: templeExteriorLocation.Id,
            worldId: WorldId,
            buildingType: BuildingType.Temple
        );
        var sanctuaryLocation = Builders.MakeLocation(WorldId, stateId: state.Id);
        var sanctuaryRoom = Builders.MakeRoom(
            temple.Id,
            worldId: WorldId,
            locationId: sanctuaryLocation.Id,
            name: TempleRoomNames.Sanctuary
        );

        _context.States.Add(state);
        _context.Cities.Add(city);
        _context.Districts.AddRange(cityEntranceDistrict, holySiteDistrict);
        _context.Locations.AddRange(deathLocation, templeExteriorLocation, sanctuaryLocation);
        _context.Buildings.Add(temple);
        _context.Rooms.Add(sanctuaryRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return sanctuaryLocation.Id;
    }
}
