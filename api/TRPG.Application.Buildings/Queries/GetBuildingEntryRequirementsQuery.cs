using Microsoft.EntityFrameworkCore;
using TRPG.Application.Buildings.Results;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetBuildingEntryRequirementsQuery
{
    public required Guid BuildingId { get; init; }
}

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
            return new BuildingEntryRequirements(BuildingEntryResult.NoEntrance, null);
        }

        var door = await GetFrontDoor(entranceRoom.LocationId, cancellationToken);
        if (door is not { IsLocked: true })
        {
            return new BuildingEntryRequirements(
                BuildingEntryResult.Entered,
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
                BuildingEntryResult.Entered,
                entranceRoom.LocationId
            );
        }

        return new BuildingEntryRequirements(
            BuildingEntryResult.Locked,
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
