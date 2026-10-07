using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Tests.Helpers;

internal sealed class WalkableTopology(Guid worldId, double walkMetersPerLocation)
{
    private readonly List<LocationConnector> _locationConnectors = [];
    private readonly List<TravelNode> _travelNodes = [];

    public IReadOnlyList<LocationConnector> LocationConnectors => _locationConnectors;

    public IReadOnlyList<TravelNode> TravelNodes => _travelNodes;

    public void ConnectBothWays(Guid firstLocationId, Guid secondLocationId)
    {
        Connect(firstLocationId, secondLocationId);
        Connect(secondLocationId, firstLocationId);
    }

    public void AddTo(DbContext context)
    {
        context.AddRange(_locationConnectors);
        context.AddRange(BuildPointConnectors());
        context.AddRange(_travelNodes);
    }

    public TravelGraph BuildGraph() =>
        new([.. _locationConnectors, .. BuildPointConnectors()], _travelNodes);

    public IReadOnlyList<PointConnector> BuildPointConnectors() =>
        [
            .. _locationConnectors
                .Select(connector => connector.DestinationLocationId)
                .Distinct()
                .SelectMany(WalkWithin),
        ];

    private void Connect(Guid originLocationId, Guid destinationLocationId)
    {
        var connector = Builders.MakeLocationConnector(
            originLocationId,
            destinationLocationId,
            worldId
        );
        _locationConnectors.Add(connector);
        _travelNodes.Add(Builders.MakeExitNode(connector));
        _travelNodes.Add(Builders.MakeArrivalNode(connector));
    }

    private IEnumerable<PointConnector> WalkWithin(Guid locationId)
    {
        var arrivals = _locationConnectors
            .Where(connector => connector.DestinationLocationId == locationId)
            .Select(connector => connector.DestinationNodeId);
        var exits = _locationConnectors
            .Where(connector => connector.OriginLocationId == locationId)
            .Select(connector => connector.OriginNodeId)
            .ToArray();

        return arrivals.SelectMany(arrival =>
            exits.Select(exit =>
                Builders.MakePointConnector(
                    locationId,
                    arrival,
                    exit,
                    walkMetersPerLocation,
                    worldId
                )
            )
        );
    }
}
