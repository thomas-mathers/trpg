using TRPG.Application.Common.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetPlacedConnectorsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetPlacedConnectorsByLocationIdQueryHandler(
    IQueryHandler<
        GetConnectorsByOriginLocationIdsQuery,
        IReadOnlyCollection<PlacedConnector>
    > getConnectorsByOrigins
) : IQueryHandler<GetPlacedConnectorsByLocationIdQuery, IReadOnlyCollection<PlacedConnector>>
{
    public async Task<IReadOnlyCollection<PlacedConnector>> Handle(
        GetPlacedConnectorsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await getConnectorsByOrigins.Handle(
            new GetConnectorsByOriginLocationIdsQuery { OriginLocationIds = [query.LocationId] },
            cancellationToken
        );
}
