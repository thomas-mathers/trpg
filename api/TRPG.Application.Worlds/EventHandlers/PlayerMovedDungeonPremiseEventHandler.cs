using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Application.Worlds.EventHandlers;

internal sealed class PlayerMovedDungeonPremiseEventHandler(
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    ICommandHandler<EnsureDungeonPremiseCommand> ensureDungeonPremise
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public async Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = domainEvent.ToLocationId },
            cancellationToken
        );
        if (building == null)
        {
            return;
        }

        await ensureDungeonPremise.Handle(
            new EnsureDungeonPremiseCommand { BuildingId = building.Id },
            cancellationToken
        );
    }
}
