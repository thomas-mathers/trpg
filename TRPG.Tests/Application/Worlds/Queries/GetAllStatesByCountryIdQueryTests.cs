using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetAllStatesByCountryIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private Country _country = null!;
    private TrpgDbContext _context = null!;
    private GetAllStatesByCountryIdQueryHandler _handler = null!;
    private State _state = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllStatesByCountryIdQueryHandler(_context);

        var world = Builders.MakeWorld();
        _country = Builders.MakeCountry(world.Id);
        _state = Builders.MakeState(_country.Id);
        _context.Worlds.Add(world);
        _context.Countries.Add(_country);
        _context.States.Add(_state);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsStatesInCountry()
    {
        // Act
        var result = await _handler.Handle(
            new GetAllStatesByCountryIdQuery { CountryId = _country.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, r => r.Id == _state.Id);
        Assert.All(result, r => Assert.Equal(_country.Id, r.CountryId));
    }
}
