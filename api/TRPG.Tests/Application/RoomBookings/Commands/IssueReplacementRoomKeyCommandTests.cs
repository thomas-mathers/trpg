using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.RoomBookings.Commands;

[Collection("Database")]
public sealed class IssueReplacementRoomKeyCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private IssueReplacementRoomKeyCommandHandler _handler = null!;
    private readonly Workstation _workstation = Builders.MakeWorkstation(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<IssueReplacementRoomKeyCommandHandler>();

        _context.Props.Add(_workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesKeyItemOwnedByWorkstationAndLinksItToTheDoor()
    {
        // Arrange
        var doorConnectorId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new IssueReplacementRoomKeyCommand
            {
                WorkstationId = _workstation.Id,
                DoorConnectorId = doorConnectorId,
                WorldId = WorldId,
                RoomName = "the Blue Room",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var doorConnectorKey = await verifyContext.DoorConnectorKeys.SingleAsync(
            key => key.DoorConnectorId == doorConnectorId,
            TestContext.Current.CancellationToken
        );
        var keyItem = await verifyContext
            .Items.OfType<Key>()
            .SingleAsync(
                item => item.Id == doorConnectorKey.ItemId,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(_workstation.Id, keyItem.Ownership.OwnerId);
        Assert.Equal(OwnerType.Workstation, keyItem.Ownership.OwnerType);
        Assert.Equal("Key to the Blue Room", keyItem.Name);
    }
}
