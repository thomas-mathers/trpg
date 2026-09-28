using TRPG.Application.Common.Commands;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.Commands;

public record ReplacementRoomKeyRequest(Guid DoorConnectorId, string RoomName);

public class IssueReplacementRoomKeysCommand
{
    public required Guid WorkstationId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<ReplacementRoomKeyRequest> Doors { get; init; }
}

internal class IssueReplacementRoomKeysCommandHandler(
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddDoorConnectorKeysCommand> addDoorConnectorKeys
) : ICommandHandler<IssueReplacementRoomKeysCommand>
{
    public async Task Handle(
        IssueReplacementRoomKeysCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Doors.Count == 0)
        {
            return;
        }

        var replacementKeysByDoor = command
            .Doors.Select(door =>
                (
                    Door: door,
                    Key: new Key
                    {
                        WorldId = command.WorldId,
                        Name = $"Key to {door.RoomName}",
                        Description = $"A replacement key to the {door.RoomName}.",
                        Quantity = 1,
                        Ownership = new ItemOwnership
                        {
                            OwnerId = command.WorkstationId,
                            OwnerType = OwnerType.Workstation,
                        },
                    }
                )
            )
            .ToArray();

        await addItems.Handle(
            new AddItemsCommand
            {
                Items = replacementKeysByDoor.Select(entry => (Item)entry.Key).ToArray(),
            },
            cancellationToken
        );

        await addDoorConnectorKeys.Handle(
            new AddDoorConnectorKeysCommand
            {
                DoorConnectorKeys = replacementKeysByDoor
                    .Select(entry => new DoorConnectorKey
                    {
                        ItemId = entry.Key.Id,
                        DoorConnectorId = entry.Door.DoorConnectorId,
                        WorldId = command.WorldId,
                    })
                    .ToArray(),
            },
            cancellationToken
        );
    }
}
