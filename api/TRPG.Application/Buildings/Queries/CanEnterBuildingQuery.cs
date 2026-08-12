using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class CanEnterBuildingQuery
{
    public required Guid BuildingId { get; init; }
    public required Guid EnteringCreatureId { get; init; }
}

internal record BuildingEntryResult(EntryOutcome Outcome, Guid? EntranceLocationId);

internal class CanEnterBuildingQueryHandler(
    TrpgDbContext context,
    GetKeyItemIdsQueryHandler getKeyItemIds,
    GetInventoryByOwnerQueryHandler getInventoryByOwner
)
{
    public async Task<BuildingEntryResult> Handle(
        CanEnterBuildingQuery buildingQuery,
        CancellationToken cancellationToken = default
    )
    {
        var entranceRoom = await context
            .Rooms.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.BuildingId == buildingQuery.BuildingId && r.FloorNumber == 0,
                cancellationToken
            );
        if (entranceRoom == null)
        {
            return new BuildingEntryResult(EntryOutcome.NoEntrance, null);
        }

        var door = await GetFrontDoor(entranceRoom.LocationId, cancellationToken);
        if (door is not { IsLocked: true })
        {
            return new BuildingEntryResult(EntryOutcome.Entered, entranceRoom.LocationId);
        }

        var validKeyItemIds = await getKeyItemIds.Handle(
            new GetKeyItemIdsQuery { DoorConnectorId = door.Id },
            cancellationToken
        );
        if (validKeyItemIds.Count == 0)
        {
            return new BuildingEntryResult(EntryOutcome.Entered, entranceRoom.LocationId);
        }

        var inventory = await getInventoryByOwner.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(
                    buildingQuery.EnteringCreatureId,
                    OwnerType.Creature
                ),
            },
            cancellationToken
        );
        var hasKey = inventory.Any(i => validKeyItemIds.Contains(i.Id));
        return hasKey
            ? new BuildingEntryResult(EntryOutcome.Entered, entranceRoom.LocationId)
            : new BuildingEntryResult(EntryOutcome.Locked, null);
    }

    private async Task<DoorConnector?> GetFrontDoor(
        Guid locationId,
        CancellationToken cancellationToken
    ) =>
        await (
            from door in context.DoorConnectors.AsNoTracking()
            join connector in context.LocationConnectors.WhereLeadsOutside(context)
                on door.ConnectorId equals connector.Id
            where connector.OriginLocationId == locationId
            select door
        ).FirstOrDefaultAsync(cancellationToken);
}
