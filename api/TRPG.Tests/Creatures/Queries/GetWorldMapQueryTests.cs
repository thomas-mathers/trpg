using Microsoft.Extensions.DependencyInjection;
using TRPG.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Creatures.Queries;

[Collection("Database")]
public sealed class GetWorldMapQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetWorldMapQueryHandler _handler = null!;

    private Country _country = null!;
    private State _playerState = null!;
    private State _ruralState = null!;
    private City _city = null!;
    private Location _playerLocation = null!;
    private Location _ruralLocation = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetWorldMapQueryHandler>();

        _country = Builders.MakeCountry(_worldId);
        _playerState = Builders.MakeState(_country.Id, _worldId);
        _ruralState = Builders.MakeState(_country.Id, _worldId);
        _city = Builders.MakeCity(_playerState.Id, _country.Id, worldId: _worldId);
        _playerLocation = Builders.MakeLocation(_worldId, stateId: _playerState.Id);
        _ruralLocation = Builders.MakeLocation(_worldId, stateId: _ruralState.Id);
        _player = Builders.MakeCreature(_worldId, locationId: _playerLocation.Id);

        _context.Countries.Add(_country);
        _context.States.AddRange(_playerState, _ruralState);
        _context.Cities.Add(_city);
        _context.Locations.AddRange(_playerLocation, _ruralLocation);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheWorldsCountriesStatesAndCities()
    {
        // Act
        var map = await _handler.Handle(
            new GetWorldMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(map.Countries, country => country.Id == _country.Id);
        Assert.Contains(map.States, state => state.Id == _playerState.Id);
        Assert.Contains(map.States, state => state.Id == _ruralState.Id);
        Assert.Contains(map.Cities, city => city.Id == _city.Id);
    }

    [Fact]
    public async Task Handle_ReturnsThePlayersCurrentState()
    {
        // Act
        var map = await _handler.Handle(
            new GetWorldMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(_playerState.Id, map.PlayerStateId);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyOneInterstateRoad_PerStatePairRegardlessOfDirection()
    {
        // Arrange — world generation links two states with a connector in each direction
        var otherLocationInPlayerState = Builders.MakeLocation(_worldId, stateId: _playerState.Id);
        var outboundRoad = Builders.MakeLocationConnector(
            _playerLocation.Id,
            _ruralLocation.Id,
            _worldId,
            name: "The King's Road"
        );
        var inboundRoad = Builders.MakeLocationConnector(
            _ruralLocation.Id,
            _playerLocation.Id,
            _worldId,
            name: "The King's Road"
        );
        var doorWithinTheSameState = Builders.MakeLocationConnector(
            _playerLocation.Id,
            otherLocationInPlayerState.Id,
            _worldId,
            name: ""
        );
        _context.Locations.Add(otherLocationInPlayerState);
        _context.LocationConnectors.AddRange(outboundRoad, inboundRoad, doorWithinTheSameState);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var map = await _handler.Handle(
            new GetWorldMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var road = Assert.Single(map.Roads);
        Assert.Equal("The King's Road", road.Name);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyUnlootedPlayerCorpses()
    {
        // Arrange
        var unlootedCorpse = Builders.MakeCreature(
            _worldId,
            locationId: _ruralLocation.Id,
            state: CreatureState.Dead,
            playerCorpseOwnerId: _player.Id,
            name: "Player's remains"
        );
        var lootedCorpse = Builders.MakeCreature(
            _worldId,
            locationId: _playerLocation.Id,
            state: CreatureState.Dead,
            playerCorpseOwnerId: _player.Id,
            name: "Looted remains"
        );
        var itemOnCorpse = Builders.MakeItem(_worldId);
        itemOnCorpse.Ownership.OwnerId = unlootedCorpse.Id;
        itemOnCorpse.Ownership.OwnerType = OwnerType.Creature;
        itemOnCorpse.Quantity = 1;
        _context.Creatures.AddRange(unlootedCorpse, lootedCorpse);
        _context.Items.Add(itemOnCorpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var map = await _handler.Handle(
            new GetWorldMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var corpse = Assert.Single(map.Corpses);
        Assert.Equal(unlootedCorpse.Id, corpse.Id);
        Assert.Equal(_ruralState.Id, corpse.StateId);
        Assert.Equal(1, corpse.ItemCount);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyIncompleteQuestObjectivesWithALocation()
    {
        // Arrange
        var quest = Builders.MakeQuest(Guid.NewGuid(), _worldId);
        var incompleteObjective = Builders.MakeExploreLocationObjective(
            quest.Id,
            _worldId,
            locationId: _ruralLocation.Id,
            name: "Investigate the ruins"
        );
        var completedObjective = Builders.MakeExploreLocationObjective(
            quest.Id,
            _worldId,
            locationId: _playerLocation.Id,
            name: "Already done"
        );
        _context.Quests.Add(quest);
        _context.QuestObjectives.AddRange(incompleteObjective, completedObjective);
        _context.CreatureQuestObjectives.AddRange(
            Builders.MakeCreatureQuestObjective(_player.Id, incompleteObjective.Id, _worldId),
            Builders.MakeCreatureQuestObjective(
                _player.Id,
                completedObjective.Id,
                _worldId,
                amount: 1
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var map = await _handler.Handle(
            new GetWorldMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var marker = Assert.Single(map.QuestMarkers);
        Assert.Equal("Investigate the ruins", marker.ObjectiveName);
        Assert.Equal(_ruralState.Id, marker.StateId);
    }
}
