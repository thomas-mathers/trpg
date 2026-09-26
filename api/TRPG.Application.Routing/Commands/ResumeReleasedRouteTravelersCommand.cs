using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Commands;

public class ResumeReleasedRouteTravelersCommand
{
    public required IReadOnlyCollection<Guid> ReleasedCreatureIds { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class ResumeReleasedRouteTravelersCommandHandler(
    IRoutingDbContext context,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds
) : ICommandHandler<ResumeReleasedRouteTravelersCommand, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        ResumeReleasedRouteTravelersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var releasedIds = command.ReleasedCreatureIds.Distinct().ToArray();
        if (releasedIds.Length == 0)
        {
            return [];
        }

        var memberships = await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => releasedIds.AsEnumerable().Contains(member.CreatureId))
            .ToArrayAsync(cancellationToken);
        if (memberships.Length == 0)
        {
            return releasedIds;
        }

        var travelerIds = memberships.Select(member => member.RouteTravelerId).Distinct().ToArray();
        var groupMemberships = await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => travelerIds.AsEnumerable().Contains(member.RouteTravelerId))
            .ToArrayAsync(cancellationToken);
        var groupCreatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = groupMemberships.Select(member => member.CreatureId).Distinct().ToArray(),
            },
            cancellationToken
        );
        var blockedTravelerIds = groupMemberships
            .Where(member => groupCreatures[member.CreatureId].IsEngaged)
            .Select(member => member.RouteTravelerId)
            .Distinct()
            .ToArray();
        var resumableTravelerIds = travelerIds.Except(blockedTravelerIds).ToArray();
        var travelers = await context
            .RouteTravelers.AsNoTracking()
            .Where(traveler =>
                resumableTravelerIds.AsEnumerable().Contains(traveler.Id)
                && traveler.PausedAtGameTime != null
            )
            .ToArrayAsync(cancellationToken);
        foreach (var traveler in travelers)
        {
            var resumedStart =
                traveler.StartedAtGameTime + (command.GameTime - traveler.PausedAtGameTime!.Value);
            await context
                .RouteTravelers.Where(candidate => candidate.Id == traveler.Id)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(candidate => candidate.StartedAtGameTime, resumedStart)
                            .SetProperty(
                                candidate => candidate.PausedAtGameTime,
                                (GameInstant?)null
                            ),
                    cancellationToken
                );
        }

        var blockedCreatureIds = memberships
            .Where(member => blockedTravelerIds.Contains(member.RouteTravelerId))
            .Select(member => member.CreatureId)
            .ToHashSet();
        return releasedIds.Where(id => !blockedCreatureIds.Contains(id)).ToArray();
    }
}
