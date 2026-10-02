using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters;
using TRPG.Application.GameTurns;
using TRPG.Application.WorldGeneration;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class GuardEncounterActionHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GuardEncounterActionHandler _handler = null!;
    private GameTurnSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .Configure<GuardEncounterOptions>(new ConfigurationBuilder().Build())
            .AddScoped<IGameClientEventDispatcher, NoOpGameClientEventDispatcher>()
            .AddScoped<IGameClientEventAckGate, NoOpGameClientEventAckGate>()
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GuardEncounterActionHandler>();

        var countryId = Guid.NewGuid();
        var state = Builders.MakeState(countryId, _worldId);
        var city = Builders.MakeCity(state.Id, countryId, worldId: _worldId);
        var cityId = city.Id;
        var encounterLocation = Builders.MakeLocation(_worldId, state.Id, cityId);
        var player = Builders.MakeCreature(_worldId, locationId: encounterLocation.Id);
        var guard = Builders.MakeCreature(
            _worldId,
            profession: Profession.Guard,
            locationId: encounterLocation.Id
        );
        var cityFaction = Builders.MakeFaction(_worldId, isCityFaction: true);
        var gameSession = Builders.MakeGameSession(_worldId, player.Id);
        _session = new GameTurnSession(gameSession.Id, _worldId, player.Id);

        var jailLocation = Builders.MakeLocation(_worldId, state.Id, cityId);
        var jail = Builders.MakeBuilding(
            exteriorLocationId: jailLocation.Id,
            worldId: _worldId,
            buildingType: BuildingType.Jail
        );
        var guardStationLocation = Builders.MakeLocation(_worldId, state.Id);
        var cellsLocation = Builders.MakeLocation(_worldId, state.Id);
        var guardStationRoom = Builders.MakeRoom(
            jail.Id,
            worldId: _worldId,
            locationId: guardStationLocation.Id,
            name: "Guard Station"
        );
        var cellsRoom = Builders.MakeRoom(
            jail.Id,
            worldId: _worldId,
            locationId: cellsLocation.Id,
            name: JailRoomNames.Cells
        );
        var exitConnector = Builders.MakeLocationConnector(
            cellsRoom.LocationId,
            destinationLocationId: guardStationRoom.LocationId
        );
        var exitDoor = Builders.MakeDoorConnector(exitConnector.Id);
        var encounter = new GuardEncounter
        {
            WorldId = _worldId,
            PlayerId = player.Id,
            LocationId = encounterLocation.Id,
            GuardCreatureId = guard.Id,
            CityFactionId = cityFaction.Id,
            GuardName = guard.Name,
            LocationName = "Market Square",
            ReputationScore = -50,
            FineAmount = 250,
            JailHours = 24,
        };

        _context.Worlds.Add(Builders.MakeWorld(_worldId, GameClock.Epoch + TimeSpan.FromHours(10)));
        _context.Creatures.AddRange(player, guard);
        _context.Factions.Add(cityFaction);
        _context.GameSessions.Add(gameSession);
        _context.States.Add(state);
        _context.Cities.Add(city);
        _context.Locations.AddRange(
            jailLocation,
            encounterLocation,
            guardStationLocation,
            cellsLocation
        );
        _context.Buildings.Add(jail);
        _context.Rooms.AddRange(guardStationRoom, cellsRoom);
        _context.LocationConnectors.Add(exitConnector);
        _context.DoorConnectors.Add(exitDoor);
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_Succeeds_WhenThePlayerGoesToJail()
    {
        // Act
        var outcome = await _handler.Handle(
            _session,
            new GoToJailEncounterAction(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(outcome.Succeeded);
    }

    private sealed class NoOpGameClientEventDispatcher : IGameClientEventDispatcher
    {
        public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class NoOpGameClientEventAckGate : IGameClientEventAckGate
    {
        public Task FlushAndAwaitAckAsync(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}
