using TRPG.Application.Common.Handling;
using TRPG.Application.Encounters.Mappers;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetHostileEncounterStateQuery
{
    public required Guid EncounterId { get; init; }
    public required Guid EncounterGroupId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetHostileEncounterStateQueryHandler(
    IQueryHandler<GetEncounterGroupContextQuery, EncounterGroupContext> getEncounterGroupContext,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById
) : IQueryHandler<GetHostileEncounterStateQuery, HostileEncounterState>
{
    public async Task<HostileEncounterState> Handle(
        GetHostileEncounterStateQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var groupContext = await getEncounterGroupContext.Handle(
            new GetEncounterGroupContextQuery { EncounterGroupId = query.EncounterGroupId },
            cancellationToken
        );
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = query.LocationId },
            cancellationToken
        );

        return HostileEncounterStateMapper.ToState(
            query.EncounterId,
            groupContext.Faction,
            groupContext.LivingMembers,
            location!
        );
    }
}
