using TRPG.Application.Buildings.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Commands;

public class SyncFrontDoorLockCommand
{
    public required Guid LocationId { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class SyncFrontDoorLockCommandHandler(
    IQueryHandler<GetBuildingByEntranceLocationQuery, Building?> getBuildingByEntranceLocation,
    ICommandHandler<SyncScheduleLockCommand, bool?> syncScheduleLock
) : ICommandHandler<SyncFrontDoorLockCommand>
{
    public async Task Handle(
        SyncFrontDoorLockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var building = await getBuildingByEntranceLocation.Handle(
            new GetBuildingByEntranceLocationQuery { LocationId = command.LocationId },
            cancellationToken
        );

        if (building == null)
        {
            return;
        }

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
