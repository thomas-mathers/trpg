using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetDoorConnectorsByConnectorIdsQuery
{
    public required IReadOnlyCollection<Guid> ConnectorIds { get; init; }
}

internal class GetDoorConnectorsByConnectorIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetDoorConnectorsByConnectorIdsQuery, IReadOnlyDictionary<Guid, DoorConnector>>
{
    public async Task<IReadOnlyDictionary<Guid, DoorConnector>> Handle(
        GetDoorConnectorsByConnectorIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .DoorConnectors.AsNoTracking()
            .Where(d => query.ConnectorIds.AsEnumerable().Contains(d.ConnectorId))
            .ToDictionaryAsync(d => d.ConnectorId, cancellationToken);
    }
}
