using TRPG.Application.Common.Commands;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.Commands;

public class IssueReplacementRoomKeyCommand
{
    public required Guid WorkstationId { get; init; }
    public required Guid DoorConnectorId { get; init; }
    public required Guid WorldId { get; init; }
    public required string RoomName { get; init; }
}

internal class IssueReplacementRoomKeyCommandHandler(
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddDoorConnectorKeyCommand> addDoorConnectorKey
) : ICommandHandler<IssueReplacementRoomKeyCommand>
{
    public async Task Handle(
        IssueReplacementRoomKeyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var replacementKey = new Key
        {
            WorldId = command.WorldId,
            Name = $"Key to {command.RoomName}",
            Description = $"A replacement key to the {command.RoomName}.",
            Quantity = 1,
            Ownership = new ItemOwnership
            {
                OwnerId = command.WorkstationId,
                OwnerType = OwnerType.Workstation,
            },
        };
        await addItems.Handle(new AddItemsCommand { Items = [replacementKey] }, cancellationToken);

        await addDoorConnectorKey.Handle(
            new AddDoorConnectorKeyCommand
            {
                DoorConnectorKey = new DoorConnectorKey
                {
                    ItemId = replacementKey.Id,
                    DoorConnectorId = command.DoorConnectorId,
                    WorldId = command.WorldId,
                },
            },
            cancellationToken
        );
    }
}
