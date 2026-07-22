using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

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
    public async Task Handle_ReturnsOnlyCreaturesInDistrict()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();

        var inDistrict = Builders.MakeCreature(worldId, districtId: districtId);
        var differentDistrict = Builders.MakeCreature(worldId, districtId: Guid.NewGuid());
        var otherWorld = Builders.MakeCreature(districtId: districtId);

        await _context.AddCreature(
            [inDistrict, differentDistrict, otherWorld],
            TestContext.Current.CancellationToken
        );

        // Act
        var results = await _handler.Handle(
            new GetCreatureIdsByDistrictQuery { WorldId = worldId, DistrictId = districtId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(inDistrict.Id, results);
        Assert.DoesNotContain(differentDistrict.Id, results);
        Assert.DoesNotContain(otherWorld.Id, results);
    }
}
