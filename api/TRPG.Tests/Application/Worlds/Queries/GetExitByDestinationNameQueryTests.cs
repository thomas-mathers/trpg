using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetExitByDestinationNameQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid StateId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetExitByDestinationNameQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding(worldId: WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetExitByDestinationNameQueryHandler>();

        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Room> SeedRoom()
    {
        var roomId = Guid.NewGuid();
        var location = Builders.MakeLocation(WorldId, StateId, roomId: roomId);
        var room = Builders.MakeRoom(
            _building.Id,
            worldId: WorldId,
            id: roomId,
            locationId: location.Id
        );
        _context.Locations.Add(location);
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return room;
    }

    private async Task<District> SeedDistrict(string name)
    {
        var districtId = Guid.NewGuid();
        var location = Builders.MakeLocation(WorldId, StateId, districtId: districtId);
        var district = Builders.MakeDistrict(
            Guid.NewGuid(),
            worldId: WorldId,
            name: name,
            id: districtId,
            locationId: location.Id
        );
        _context.Locations.Add(location);
        _context.Districts.Add(district);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return district;
    }

    [Fact]
    public async Task Handle_MatchesByDestinationRoomName_ForARoomToLocationConnector()
    {
        // Arrange
        var room = await SeedRoom();
        var destinationRoom = await SeedRoom();
        var connector = Builders.MakeLocationConnector(
            room.LocationId,
            destinationLocationId: destinationRoom.LocationId,
            worldId: WorldId,
            name: "Hallway",
            description: "A hallway.",
            destinationLabel: destinationRoom.Name
        );
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetExitByDestinationNameQuery
            {
                LocationId = room.LocationId,
                DestinationName = destinationRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.Matched);
        Assert.Equal(destinationRoom.LocationId, result.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_MatchesOutside_ForABuildingFrontDoor_LeadingToItsOwnDistrict()
    {
        // Arrange - the front door's destination is a district's real outdoor Location; the
        // player is indoors, so the label is the generic "Outside", not the district's own name
        var room = await SeedRoom();
        var district = await SeedDistrict("Market Row");
        var frontDoor = Builders.MakeLocationConnector(
            room.LocationId,
            destinationLocationId: district.LocationId,
            worldId: WorldId,
            name: "Front Door",
            description: "The door leading outside."
        );
        _context.LocationConnectors.Add(frontDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetExitByDestinationNameQuery
            {
                LocationId = room.LocationId,
                DestinationName = "Outside",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.Matched);
        Assert.Equal(district.LocationId, result.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_MatchesOutside_ForADungeonFrontDoor_LeadingToTheWilderness()
    {
        // Arrange - dungeons have no district, so the front door leads to a shared
        // state-level wilderness Location (no city/district/room) rather than a district
        var room = await SeedRoom();
        var wilderness = Builders.MakeLocation(WorldId, StateId);
        var frontDoor = Builders.MakeLocationConnector(
            room.LocationId,
            destinationLocationId: wilderness.Id,
            worldId: WorldId,
            name: "Front Door",
            description: "The way back outside."
        );
        _context.Locations.Add(wilderness);
        _context.LocationConnectors.Add(frontDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetExitByDestinationNameQuery
            {
                LocationId = room.LocationId,
                DestinationName = "Outside",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.Matched);
        Assert.Equal(wilderness.Id, result.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_MatchesByAdjacentDistrictName_ForAHubConnector()
    {
        // Arrange - outdoors, a hub connector leads to another district; the label is that
        // district's real name, not "Outside"
        var origin = await SeedDistrict("Docks");
        var cityCenter = await SeedDistrict("City Center");
        var connector = Builders.MakeLocationConnector(
            origin.LocationId,
            destinationLocationId: cityCenter.LocationId,
            worldId: WorldId,
            name: "Path",
            description: "A path leading to City Center.",
            destinationLabel: cityCenter.Name
        );
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetExitByDestinationNameQuery
            {
                LocationId = origin.LocationId,
                DestinationName = "City Center",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.Matched);
        Assert.Equal(cityCenter.LocationId, result.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_ReturnsNotMatched_WhenNoConnectorMatchesTheName()
    {
        // Arrange
        var room = await SeedRoom();

        // Act
        var result = await _handler.Handle(
            new GetExitByDestinationNameQuery
            {
                LocationId = room.LocationId,
                DestinationName = "Nowhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.Matched);
        Assert.Null(result.DestinationLocationId);
    }
}
