using TRPG.Application.WorldGeneration;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetJailForCityQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetJailForCityQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetJailForCityQueryHandler(_context);
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheCellsLocationAndExitDoor_WhenTheCityHasAJail()
    {
        // Arrange
        var cityId = Guid.NewGuid();
        var jailLocation = Builders.MakeLocation(cityId: cityId);
        var jail = Builders.MakeBuilding(
            exteriorLocationId: jailLocation.Id,
            buildingType: BuildingType.Jail
        );
        var guardStationRoom = Builders.MakeRoom(jail.Id, name: "Guard Station");
        var cellsRoom = Builders.MakeRoom(jail.Id, name: JailRoomNames.Cells);
        var exitConnector = Builders.MakeLocationConnector(
            cellsRoom.LocationId,
            destinationLocationId: guardStationRoom.LocationId
        );
        var exitDoor = Builders.MakeDoorConnector(exitConnector.Id, isLocked: true);
        _context.Locations.Add(jailLocation);
        _context.Buildings.Add(jail);
        _context.Rooms.AddRange(guardStationRoom, cellsRoom);
        _context.LocationConnectors.Add(exitConnector);
        _context.DoorConnectors.Add(exitDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetJailForCityQuery { CityId = cityId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(cellsRoom.LocationId, result.CellsLocationId);
        Assert.Equal(exitDoor.Id, result.ExitDoorConnectorId);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheCityHasNoJail()
    {
        // Act
        var result = await _handler.Handle(
            new GetJailForCityQuery { CityId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
