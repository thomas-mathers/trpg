using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Factions.Queries;

public sealed record FactionLeadership(Guid? LeaderCreatureId, int MemberCount);

public class GetFactionLeadershipQuery
{
    public required Guid FactionId { get; init; }
}

internal class GetFactionLeadershipQueryHandler(IFactionsDbContext context)
    : IQueryHandler<GetFactionLeadershipQuery, FactionLeadership>
{
    public async Task<FactionLeadership> Handle(
        GetFactionLeadershipQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var leaderCreatureId = await context
            .FactionMembers.AsNoTracking()
            .Where(fm => fm.FactionId == query.FactionId && fm.Role == FactionRole.Leader)
            .Select(fm => (Guid?)fm.CreatureId)
            .FirstOrDefaultAsync(cancellationToken);

        var memberCount = await context.FactionMembers.CountAsync(
            fm => fm.FactionId == query.FactionId,
            cancellationToken
        );

        return new FactionLeadership(leaderCreatureId, memberCount);
    }
}
