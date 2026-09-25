using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncFrontDoorLockCommand
{
    public required Guid LocationId { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class SyncFrontDoorLockCommandHandler(
    IQueryHandler<GetBuildingByEntranceLocationQuery, Building?> getBuildingByEntranceLocation,
    IQueryHandler<
        GetBuildingsByLocationQuery,
        IReadOnlyCollection<Building>
    > getBuildingsByLocation,
    ICommandHandler<SyncScheduleLockCommand, bool?> syncScheduleLock
) : ICommandHandler<SyncFrontDoorLockCommand>
{
    public async Task Handle(
        SyncFrontDoorLockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var entranceBuilding = await getBuildingByEntranceLocation.Handle(
            new GetBuildingByEntranceLocationQuery { LocationId = command.LocationId },
            cancellationToken
        );
        var exteriorBuildings = await getBuildingsByLocation.Handle(
            new GetBuildingsByLocationQuery { LocationId = command.LocationId },
            cancellationToken
        );
        var buildings = exteriorBuildings
            .Append(entranceBuilding)
            .Where(building => building != null)
            .Select(building => building!)
            .DistinctBy(building => building.Id)
            .ToArray();

        foreach (var building in buildings)
        {
            await syncScheduleLock.Handle(
                new SyncScheduleLockCommand
                {
                    BuildingId = building.Id,
                    BuildingType = building.BuildingType,
                    CurrentDate = command.CurrentDate,
                },
                cancellationToken
            );
        }
    }
}
