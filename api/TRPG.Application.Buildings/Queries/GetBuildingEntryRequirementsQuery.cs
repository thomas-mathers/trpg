using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

public enum BuildingEntryOutcome
{
    Entered,
    NoEntrance,
    Locked,
}

public class GetBuildingEntryRequirementsQuery
{
    public required Guid BuildingId { get; init; }
}

public record BuildingEntryRequirements(
    BuildingEntryOutcome Outcome,
    Guid? EntranceLocationId,
    IReadOnlyCollection<Guid>? ValidKeyItemIds = null
);

internal class GetBuildingEntryRequirementsQueryHandler(
    TrpgDbContext context,
    IQueryHandler<GetKeyItemIdsQuery, IReadOnlyList<Guid>> getKeyItemIds
) : IQueryHandler<GetBuildingEntryRequirementsQuery, BuildingEntryRequirements>
{
    public async Task<BuildingEntryRequirements> Handle(
        GetBuildingEntryRequirementsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var entranceRoom = await context
            .Rooms.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.BuildingId == query.BuildingId && r.FloorNumber == 0,
                cancellationToken
            );
        if (entranceRoom == null)
        {
            return new BuildingEntryRequirements(BuildingEntryOutcome.NoEntrance, null);
        }

        var door = await GetFrontDoor(entranceRoom.LocationId, cancellationToken);
        if (door is not { IsLocked: true })
        {
            return new BuildingEntryRequirements(
                BuildingEntryOutcome.Entered,
                entranceRoom.LocationId
            );
        }

        var validKeyItemIds = await getKeyItemIds.Handle(
            new GetKeyItemIdsQuery { DoorConnectorId = door.Id },
            cancellationToken
        );
        if (validKeyItemIds.Count == 0)
        {
            return new BuildingEntryRequirements(
                BuildingEntryOutcome.Entered,
                entranceRoom.LocationId
            );
        }

        return new BuildingEntryRequirements(
            BuildingEntryOutcome.Locked,
            entranceRoom.LocationId,
            validKeyItemIds
        );
    }

    private async Task<DoorConnector?> GetFrontDoor(
        Guid locationId,
        CancellationToken cancellationToken
    ) =>
        await (
            from door in context.DoorConnectors.AsNoTracking()
            join connector in context.LocationConnectors.AsNoTracking()
                on door.ConnectorId equals connector.Id
            join destination in context.Locations.AsNoTracking()
                on connector.DestinationLocationId equals destination.Id
            where connector.OriginLocationId == locationId && destination.RoomId == null
            select door
        ).FirstOrDefaultAsync(cancellationToken);
}
