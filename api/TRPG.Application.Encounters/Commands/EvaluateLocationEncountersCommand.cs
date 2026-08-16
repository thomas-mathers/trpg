using Microsoft.EntityFrameworkCore;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Mappers;
using TRPG.Application.Encounters.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateLocationEncountersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public Guid? OriginLocationId { get; init; }
}

internal class EvaluateLocationEncountersCommandHandler(
    TrpgDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetActiveEncounterQuery, HostileEncounter?> getActiveEncounter,
    IQueryHandler<GetActiveFightQuery, Fight?> getActiveFight
) : ICommandHandler<EvaluateLocationEncountersCommand, HostileEncounterState?>
{
    public async Task<HostileEncounterState?> Handle(
        EvaluateLocationEncountersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return null;
        }

        var activeFight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeFight != null)
        {
            return null;
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var groups = await context
            .EncounterGroups.AsNoTracking()
            .Where(g => g.WorldId == command.WorldId && g.LocationId == player!.LocationId)
            .ToArrayAsync(cancellationToken);
        if (groups.Length == 0)
        {
            return null;
        }

        var groupIds = groups.Select(g => g.Id).ToArray();

        var members = await context
            .EncounterGroupMembers.AsNoTracking()
            .Where(m => groupIds.AsEnumerable().Contains(m.EncounterGroupId))
            .ToArrayAsync(cancellationToken);

        var memberCreatureIds = members.Select(m => m.CreatureId).ToArray();

        var livingCreaturesById = await context
            .Creatures.AsNoTracking()
            .Where(c =>
                memberCreatureIds.AsEnumerable().Contains(c.Id)
                && c.State != CreatureState.Dead
                && c.State != CreatureState.Sleeping
            )
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var factionIds = groups.Select(g => g.FactionId).Distinct().ToArray();

        var factionsById = await context
            .Factions.AsNoTracking()
            .Where(f => factionIds.AsEnumerable().Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        var reputationByFactionId = await context
            .Reputations.AsNoTracking()
            .Where(r =>
                r.CreatureId == command.PlayerId
                && r.TargetType == ReputationTargetType.Faction
                && factionIds.AsEnumerable().Contains(r.TargetId)
            )
            .ToDictionaryAsync(r => r.TargetId, r => r.Score, cancellationToken);

        var candidates = groups
            .Select(group =>
                BuildCandidate(
                    group,
                    members,
                    livingCreaturesById,
                    factionsById,
                    reputationByFactionId
                )
            )
            .ToArray();

        var selectedGroupId = HostileEncounterInitiationResolver.Resolve(player!.Level, candidates);
        if (selectedGroupId == null)
        {
            return null;
        }

        var selectedGroup = groups.First(g => g.Id == selectedGroupId.Value);
        var selectedFaction = factionsById[selectedGroup.FactionId];
        var selectedLivingMembers = members
            .Where(m => m.EncounterGroupId == selectedGroupId.Value)
            .Where(m => livingCreaturesById.ContainsKey(m.CreatureId))
            .Select(m => livingCreaturesById[m.CreatureId])
            .ToArray();

        var location = await context
            .Locations.AsNoTracking()
            .FirstAsync(l => l.Id == player.LocationId, cancellationToken);

        var encounter = new HostileEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = player.LocationId,
            ArrivalOriginLocationId = command.OriginLocationId,
            EncounterGroupId = selectedGroupId.Value,
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return selectedFaction.ToState(encounter.Id, selectedLivingMembers, location);
    }

    private static HostileEncounterCandidateGroup BuildCandidate(
        EncounterGroup group,
        IReadOnlyCollection<EncounterGroupMember> members,
        IReadOnlyDictionary<Guid, Creature> livingCreaturesById,
        IReadOnlyDictionary<Guid, Faction> factionsById,
        IReadOnlyDictionary<Guid, int> reputationByFactionId
    )
    {
        var faction = factionsById[group.FactionId];
        var livingMemberLevels = members
            .Where(m => m.EncounterGroupId == group.Id)
            .Where(m => livingCreaturesById.ContainsKey(m.CreatureId))
            .Select(m => livingCreaturesById[m.CreatureId].Level)
            .ToArray();

        return new HostileEncounterCandidateGroup(
            GroupId: group.Id,
            Aggression: faction.Aggression,
            ReputationSensitivity: faction.ReputationSensitivity,
            RiskAversion: faction.RiskAversion,
            ReputationScore: reputationByFactionId.GetValueOrDefault(group.FactionId, 0),
            LivingMemberLevels: livingMemberLevels
        );
    }
}
