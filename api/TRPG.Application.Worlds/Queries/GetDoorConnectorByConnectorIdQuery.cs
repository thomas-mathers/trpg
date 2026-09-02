using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetDoorConnectorByConnectorIdQuery
{
    public required Guid ConnectorId { get; init; }
}

internal class GetDoorConnectorByConnectorIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetDoorConnectorByConnectorIdQuery, DoorConnector?>
{
    public async Task<DoorConnector?> Handle(
        GetDoorConnectorByConnectorIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .DoorConnectors.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ConnectorId == query.ConnectorId, cancellationToken);
    }
}
