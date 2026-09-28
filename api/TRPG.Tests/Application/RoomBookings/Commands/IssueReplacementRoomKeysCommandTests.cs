using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.RoomBookings.Commands;

public sealed class IssueReplacementRoomKeysCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private IssueReplacementRoomKeysCommandHandler _handler = null!;
    private readonly Workstation _workstation = Builders.MakeWorkstation(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<IssueReplacementRoomKeysCommandHandler>();

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
            new IssueReplacementRoomKeysCommand
            {
                WorkstationId = _workstation.Id,
                WorldId = WorldId,
                Doors = [new ReplacementRoomKeyRequest(doorConnectorId, "the Blue Room")],
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

    [Fact]
    public async Task Handle_LinksEachKeyToItsOwnDoor_WhenMultipleDoorsAreRequested()
    {
        // Arrange — guards the batched insert against mixing up which key belongs to which door
        var firstDoorConnectorId = Guid.NewGuid();
        var secondDoorConnectorId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new IssueReplacementRoomKeysCommand
            {
                WorkstationId = _workstation.Id,
                WorldId = WorldId,
                Doors =
                [
                    new ReplacementRoomKeyRequest(firstDoorConnectorId, "the Blue Room"),
                    new ReplacementRoomKeyRequest(secondDoorConnectorId, "the Red Room"),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var firstDoorKey = await verifyContext.DoorConnectorKeys.SingleAsync(
            key => key.DoorConnectorId == firstDoorConnectorId,
            TestContext.Current.CancellationToken
        );
        var secondDoorKey = await verifyContext.DoorConnectorKeys.SingleAsync(
            key => key.DoorConnectorId == secondDoorConnectorId,
            TestContext.Current.CancellationToken
        );
        var firstKeyItem = await verifyContext
            .Items.OfType<Key>()
            .SingleAsync(
                item => item.Id == firstDoorKey.ItemId,
                TestContext.Current.CancellationToken
            );
        var secondKeyItem = await verifyContext
            .Items.OfType<Key>()
            .SingleAsync(
                item => item.Id == secondDoorKey.ItemId,
                TestContext.Current.CancellationToken
            );
        Assert.Equal("Key to the Blue Room", firstKeyItem.Name);
        Assert.Equal("Key to the Red Room", secondKeyItem.Name);
    }
}
