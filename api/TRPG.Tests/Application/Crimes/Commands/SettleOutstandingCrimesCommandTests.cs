using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Commands;

[Collection("Database")]
public sealed class SettleOutstandingCrimesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid CityId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SettleOutstandingCrimesCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SettleOutstandingCrimesCommandHandler>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SettlesReportedCrimesInTheCity()
    {
        // Arrange
        var crime = SeedTheft(CityId, CrimeResolution.Reported);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(persisted!.SettledAt);
    }

    [Fact]
    public async Task Handle_LeavesCrimesInAnotherCityOutstanding()
    {
        // Arrange
        var crime = SeedTheft(Guid.NewGuid(), CrimeResolution.Reported);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Null(persisted!.SettledAt);
    }

    [Fact]
    public async Task Handle_LeavesUnreportedCrimesOutstanding()
    {
        // Arrange
        var crime = SeedTheft(CityId, CrimeResolution.Unreported);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Null(persisted!.SettledAt);
    }

    private SettleOutstandingCrimesCommand MakeCommand() =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CityId = CityId,
        };

    private TheftCrime SeedTheft(Guid cityId, CrimeResolution resolution)
    {
        var crime = new TheftCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = Guid.NewGuid(),
            CityId = cityId,
            Resolution = resolution,
            OwnerCreatureId = Guid.NewGuid(),
            OwnerName = "Cora",
        };
        _context.Crimes.Add(crime);
        return crime;
    }
}
