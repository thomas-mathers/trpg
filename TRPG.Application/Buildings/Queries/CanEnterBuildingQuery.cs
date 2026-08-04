using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common;
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
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId
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
            new GetKeyItemIdsQuery { LocationConnectorId = door.Id },
            cancellationToken
        );
        if (validKeyItemIds.Count == 0)
        {
            return new BuildingEntryResult(EntryOutcome.Entered, entranceRoom.LocationId);
        }

        var inventory = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = buildingQuery.EnteringCreatureId },
            cancellationToken
        );
        var hasKey = inventory.Any(i => validKeyItemIds.Contains(i.Id));
        return hasKey
            ? new BuildingEntryResult(EntryOutcome.Entered, entranceRoom.LocationId)
            : new BuildingEntryResult(EntryOutcome.Locked, null);
    }

    private async Task<LocationConnector?> GetFrontDoor(
        Guid locationId,
        CancellationToken cancellationToken
    ) =>
        await context
            .Props.AsNoTracking()
            .Where(p => p.LocationId == locationId)
            .OfType<LocationConnector>()
            .WhereLeadsOutside(context)
            .FirstOrDefaultAsync(cancellationToken);
}
