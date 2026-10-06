using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Neighbors;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Scenes.Roads;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Queries;

public class GetSceneNeighborsQuery
{
    public required Guid LocationId { get; init; }
    public required Footprint Size { get; init; }
    public required IReadOnlyCollection<SceneExitInfo> Exits { get; init; }
    public required IReadOnlyCollection<SceneNearbyBuildingInfo> Buildings { get; init; }
}

internal class GetSceneNeighborsQueryHandler(
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<
        GetConnectorsByLocationIdQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectorsByLocationId,
    IQueryHandler<
        GetBuildingsByLocationQuery,
        IReadOnlyCollection<Building>
    > getAllBuildingsByLocation,
    IQueryHandler<GetPropsByLocationIdQuery, IReadOnlyCollection<Prop>> getPropsByLocationId,
    IQueryHandler<GetRoadNetworkByLocationIdQuery, LocationRoadNetwork> getRoadNetwork
) : IQueryHandler<GetSceneNeighborsQuery, IReadOnlyCollection<NeighborDistrict>>
{
    public async Task<IReadOnlyCollection<NeighborDistrict>> Handle(
        GetSceneNeighborsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var neighbors = new List<NeighborDistrict>();

        foreach (var exit in DistrictEdgeExits(query.Size, query.Exits))
        {
            var source = await LoadSource(
                query.LocationId,
                exit.DestinationLocationId,
                cancellationToken
            );
            if (source == null)
            {
                continue;
            }

            var neighbor = NeighborPreviewPlanner.Plan(query.Size, exit.Placement, source);
            if (
                neighbor != null
                && !neighbors.Any(placed => NeighborPreviewPlanner.Overlaps(placed, neighbor))
            )
            {
                neighbors.Add(neighbor);
            }
        }

        var sealedNeighbors = DistrictCornerPlanner.Seal(
            query.Size,
            NeighborWallTrimmer.Trim(neighbors)
        );

        return NeighborWallTrimmer.TrimAround(sealedNeighbors, query.Buildings);
    }

    private static IEnumerable<SceneExitInfo> DistrictEdgeExits(
        Footprint size,
        IReadOnlyCollection<SceneExitInfo> exits
    ) =>
        exits.Where(exit =>
            exit.Destination is SceneDistrictExitDestination
            && DistrictBoundaryPlanner.EdgeOf(size, exit.Placement) != null
        );

    private async Task<NeighborSource?> LoadSource(
        Guid locationId,
        Guid neighborId,
        CancellationToken cancellationToken
    )
    {
        var connectors = await getConnectorsByLocationId.Handle(
            new GetConnectorsByLocationIdQuery { LocationId = neighborId },
            cancellationToken
        );
        var reverse = connectors.FirstOrDefault(connector =>
            connector.DestinationLocationId == locationId
        );
        if (reverse == null)
        {
            return null;
        }

        var locationIds = connectors
            .Select(connector => connector.DestinationLocationId)
            .Append(neighborId)
            .Distinct()
            .ToArray();
        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = locationIds },
            cancellationToken
        );

        var buildings = await getAllBuildingsByLocation.Handle(
            new GetBuildingsByLocationQuery { LocationId = neighborId },
            cancellationToken
        );

        var props = await getPropsByLocationId.Handle(
            new GetPropsByLocationIdQuery { LocationId = neighborId },
            cancellationToken
        );

        var network = await getRoadNetwork.Handle(
            new GetRoadNetworkByLocationIdQuery { LocationId = neighborId },
            cancellationToken
        );

        var neighbor = locations[neighborId];

        return new NeighborSource(
            neighborId,
            new Footprint(neighbor.Width, neighbor.Depth),
            ExitPlacement(reverse),
            connectors
                .Select(connector => ToBoundaryExit(connector, locations))
                .OfType<BoundaryExit>()
                .ToArray(),
            buildings.Select(ToNearbyBuilding).ToArray(),
            props.Where(IsVisibleFromAfar).Select(ToNearbyProp).ToArray(),
            SceneRoadMapper.ToRoads(network)
        );
    }

    private static Placement ExitPlacement(LocationConnector connector) =>
        new(connector.ExitX, connector.ExitY, connector.ExitAngle);

    private static BoundaryExit? ToBoundaryExit(
        LocationConnector connector,
        IReadOnlyDictionary<Guid, Location> locations
    ) =>
        locations.GetValueOrDefault(connector.DestinationLocationId)?.Kind switch
        {
            LocationKind.District => new BoundaryExit(
                connector.Id,
                BoundaryExitKind.District,
                ExitPlacement(connector)
            ),
            LocationKind.Wilderness => new BoundaryExit(
                connector.Id,
                BoundaryExitKind.Wilderness,
                ExitPlacement(connector)
            ),
            _ => null,
        };

    private static bool IsVisibleFromAfar(Prop prop) => prop is not (Trap or Trigger);

    private static ScenePropInfo ToNearbyProp(Prop prop) =>
        new(
            prop.Id,
            prop.Name,
            prop.Description,
            prop.GetType().Name,
            IsOccupied: false,
            IsOccupiedByPlayer: false,
            Model: PropModelResolver.Resolve(prop),
            Placement: new Placement(prop.X, prop.Y, prop.Angle),
            Footprint: new Footprint(prop.Width, prop.Depth)
        );

    private static SceneNearbyBuildingInfo ToNearbyBuilding(Building building) =>
        new(
            building.Id,
            building.Name,
            building.BuildingType,
            new Placement(building.X, building.Y, building.Angle),
            new Footprint(building.Width, building.Depth),
            building.FloorCount
        );
}
