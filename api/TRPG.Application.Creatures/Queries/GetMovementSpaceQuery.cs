using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetMovementSpaceQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetMovementSpaceQueryHandler(
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetPropsByLocationIdQuery, IReadOnlyCollection<Prop>> getPropsByLocation,
    IQueryHandler<
        GetBuildingsByLocationQuery,
        IReadOnlyCollection<Building>
    > getBuildingsByLocation,
    IQueryHandler<
        GetPlacedConnectorsByLocationIdQuery,
        IReadOnlyCollection<PlacedConnector>
    > getConnectorsByLocation
) : IQueryHandler<GetMovementSpaceQuery, MovementSpace?>
{
    public async Task<MovementSpace?> Handle(
        GetMovementSpaceQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = [query.LocationId] },
            cancellationToken
        );
        if (!locations.TryGetValue(query.LocationId, out var location))
        {
            return null;
        }

        var props = await getPropsByLocation.Handle(
            new GetPropsByLocationIdQuery { LocationId = query.LocationId },
            cancellationToken
        );
        var buildings = await getBuildingsByLocation.Handle(
            new GetBuildingsByLocationQuery { LocationId = query.LocationId },
            cancellationToken
        );
        var connectors = await getConnectorsByLocation.Handle(
            new GetPlacedConnectorsByLocationIdQuery { LocationId = query.LocationId },
            cancellationToken
        );

        return MovementSpaceBuilder.Build(location, props, buildings, connectors);
    }
}
