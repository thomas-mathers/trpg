using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureIdsByDistrictQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreatureIdsByDistrictQueryHandler _handler = null!;
    private readonly Guid _worldId = Guid.NewGuid();

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
        var districtId = Guid.NewGuid();

        var inDistrict = Builders.MakeCreature(_worldId, districtId: districtId);
        var differentDistrict = Builders.MakeCreature(_worldId, districtId: Guid.NewGuid());
        var otherWorld = Builders.MakeCreature(districtId: districtId);

        _context.Creatures.AddRange(inDistrict, differentDistrict, otherWorld);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await _handler.Handle(
            new GetCreatureIdsByDistrictQuery { WorldId = _worldId, DistrictId = districtId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(inDistrict.Id, results);
        Assert.DoesNotContain(differentDistrict.Id, results);
        Assert.DoesNotContain(otherWorld.Id, results);
    }
}
