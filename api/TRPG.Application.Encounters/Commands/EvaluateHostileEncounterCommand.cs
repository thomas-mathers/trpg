using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateHostileEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class EvaluateHostileEncounterCommandHandler(
    TrpgDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>> getFactionsByIds
) : ICommandHandler<EvaluateHostileEncounterCommand, HostileEncounter?>
{
    public async Task<HostileEncounter?> Handle(
        EvaluateHostileEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
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

        var factionsById = await getFactionsByIds.Handle(
            new GetFactionsByIdsQuery { Ids = factionIds },
            cancellationToken
        );

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
            FactionId = selectedFaction.Id,
            FactionName = selectedFaction.Name,
            LocationName = location.Name,
            Members = selectedLivingMembers
                .Select(member => new HostileEncounterMemberSnapshot(
                    member.Id,
                    member.Name,
                    member.CreatureType,
                    member.Level
                ))
                .ToList(),
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return encounter;
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
