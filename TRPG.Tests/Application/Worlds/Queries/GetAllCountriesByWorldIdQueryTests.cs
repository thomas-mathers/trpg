using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetAllCountriesByWorldIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private Country _country = null!;
    private TrpgDbContext _context = null!;
    private GetAllCountriesByWorldIdQueryHandler _handler = null!;
    private World _world = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllCountriesByWorldIdQueryHandler(_context);

        _world = Builders.MakeWorld();
        _country = Builders.MakeCountry(_world.Id);
        _context.Worlds.Add(_world);
        _context.Countries.Add(_country);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCountriesInWorld()
    {
        // Act
        var result = await _handler.Handle(
            new GetAllCountriesByWorldIdQuery { WorldId = _world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, c => c.Id == _country.Id);
        Assert.All(result, c => Assert.Equal(_world.Id, c.WorldId));
    }
}
