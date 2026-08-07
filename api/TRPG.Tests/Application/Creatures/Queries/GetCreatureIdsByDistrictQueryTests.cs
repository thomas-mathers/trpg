using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureIdsByDistrictQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreatureIdsByDistrictQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureIdsByDistrictQueryHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCreaturesInDistrict_RegardlessOfWhichRoomOrOutdoorSpot()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var outdoorLocation = Builders.MakeLocation(worldId, districtId: districtId);
        var indoorLocation = Builders.MakeLocation(
            worldId,
            districtId: districtId,
            roomId: Guid.NewGuid()
        );
        var differentDistrictLocation = Builders.MakeLocation(worldId, districtId: Guid.NewGuid());
        _context.Locations.AddRange(outdoorLocation, indoorLocation, differentDistrictLocation);

        var outdoors = Builders.MakeCreature(worldId, locationId: outdoorLocation.Id);
        var indoors = Builders.MakeCreature(worldId, locationId: indoorLocation.Id);
        var differentDistrict = Builders.MakeCreature(
            worldId,
            locationId: differentDistrictLocation.Id
        );
        var otherWorld = Builders.MakeCreature(locationId: outdoorLocation.Id);

        _context.Creatures.AddRange(outdoors, indoors, differentDistrict, otherWorld);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await _handler.Handle(
            new GetCreatureIdsByDistrictQuery { WorldId = worldId, DistrictId = districtId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(outdoors.Id, results);
        Assert.Contains(indoors.Id, results);
        Assert.DoesNotContain(differentDistrict.Id, results);
        Assert.DoesNotContain(otherWorld.Id, results);
    }
}
