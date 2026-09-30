using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.EventHandlers;

internal sealed class WorkstationRestockedReplacementKeyEventHandler(
    IQueryHandler<GetBuildingByIdQuery, Building?> getBuildingById,
    IQueryHandler<
        GetGuestRoomDoorsByBuildingIdQuery,
        IReadOnlyList<GuestRoomDoor>
    > getGuestRoomDoors,
    IQueryHandler<
        GetWorkstationOwnedItemIdsQuery,
        IReadOnlyDictionary<Guid, Guid>
    > getWorkstationOwnedItemIds,
    ICommandHandler<IssueReplacementRoomKeysCommand> issueReplacementRoomKeys
) : IDomainEventConsumer<WorkstationRestockedEvent>
{
    public async Task Handle(
        WorkstationRestockedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var building = await getBuildingById.Handle(
            new GetBuildingByIdQuery { Id = domainEvent.BuildingId },
            cancellationToken
        );
        if (building?.BuildingType != BuildingType.Inn)
        {
            return;
        }

        var doorsNeedingKeys = await GetDoorsMissingKeys(domainEvent.BuildingId, cancellationToken);

        await issueReplacementRoomKeys.Handle(
            new IssueReplacementRoomKeysCommand
            {
                WorkstationId = domainEvent.WorkstationId,
                WorldId = domainEvent.WorldId,
                Doors = doorsNeedingKeys,
            },
            cancellationToken
        );
    }

    // Never revokes an already-issued key.
    private async Task<ReplacementRoomKeyRequest[]> GetDoorsMissingKeys(
        Guid buildingId,
        CancellationToken cancellationToken
    )
    {
        var guestRoomDoors = await getGuestRoomDoors.Handle(
            new GetGuestRoomDoorsByBuildingIdQuery { BuildingId = buildingId },
            cancellationToken
        );
        var candidateKeyItemIds = guestRoomDoors
            .SelectMany(door => door.CandidateKeyItemIds)
            .ToArray();
        var workstationIdsByItemId = await getWorkstationOwnedItemIds.Handle(
            new GetWorkstationOwnedItemIdsQuery { ItemIds = candidateKeyItemIds },
            cancellationToken
        );

        return guestRoomDoors
            .Where(door =>
                !door.CandidateKeyItemIds.Any(id => workstationIdsByItemId.ContainsKey(id))
            )
            .Select(door => new ReplacementRoomKeyRequest(door.DoorConnectorId, door.RoomName))
            .ToArray();
    }
}
