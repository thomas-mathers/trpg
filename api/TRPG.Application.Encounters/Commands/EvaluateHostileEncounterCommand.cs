using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateHostileEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class EvaluateHostileEncounterCommandHandler(
    IEncountersDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>> getFactionsByIds,
    IQueryHandler<
        GetReputationsByCreatureIdQuery,
        IReadOnlyCollection<Reputation>
    > getReputationsByCreatureId,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    ICommandHandler<CreateHostileEncounterCommand, HostileEncounter> createHostileEncounter
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

        var candidateCreaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = memberCreatureIds },
            cancellationToken
        );
        var livingCreaturesById = candidateCreaturesById
            .Where(kv =>
                kv.Value.State != CreatureState.Dead && kv.Value.State != CreatureState.Sleeping
            )
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var factionIds = groups.Select(g => g.FactionId).Distinct().ToArray();

        var factionsById = await getFactionsByIds.Handle(
            new GetFactionsByIdsQuery { Ids = factionIds },
            cancellationToken
        );

        var playerReputations = await getReputationsByCreatureId.Handle(
            new GetReputationsByCreatureIdQuery { CreatureId = command.PlayerId },
            cancellationToken
        );
        var reputationByFactionId = playerReputations
            .Where(r =>
                r.TargetType == ReputationTargetType.Faction && factionIds.Contains(r.TargetId)
            )
            .ToDictionary(r => r.TargetId, r => r.Score);

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

        var location =
            await getLocationById.Handle(
                new GetLocationByIdQuery { Id = player.LocationId },
                cancellationToken
            ) ?? throw new InvalidOperationException($"Location {player.LocationId} not found.");

        return await createHostileEncounter.Handle(
            new CreateHostileEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                PlayerLocationId = player.LocationId,
                LocationName = location.Name,
                FactionId = selectedFaction.Id,
                FactionName = selectedFaction.Name,
                Members = selectedLivingMembers
                    .Select(member => new HostileEncounterMemberSnapshot(
                        member.Id,
                        member.Name,
                        member.CreatureType,
                        member.Level
                    ))
                    .ToArray(),
            },
            cancellationToken
        );
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
