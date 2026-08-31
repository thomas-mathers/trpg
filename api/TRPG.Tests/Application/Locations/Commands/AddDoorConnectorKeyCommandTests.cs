using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Locations.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Locations.Commands;

[Collection("Database")]
public sealed class AddDoorConnectorKeyCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AddDoorConnectorKeyCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AddDoorConnectorKeyCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesTheDoorConnectorKey()
    {
        // Arrange
        var key = new DoorConnectorKey
        {
            ItemId = Guid.NewGuid(),
            DoorConnectorId = Guid.NewGuid(),
            WorldId = WorldId,
        };

        // Act
        await _handler.Handle(
            new AddDoorConnectorKeyCommand { DoorConnectorKey = key },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.DoorConnectorKeys.SingleAsync(
            k => k.Id == key.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(key.ItemId, persisted.ItemId);
        Assert.Equal(key.DoorConnectorId, persisted.DoorConnectorId);
    }
}
