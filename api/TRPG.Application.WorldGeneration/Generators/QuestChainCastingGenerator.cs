using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record QuestChainHandoff(string UpstreamNodeId, Guid NextGiverId, string NextGiverName);

public record QuestChainCast(
    IReadOnlyDictionary<string, Guid> GiverIdByNodeId,
    Guid? GiverFactionId,
    string? GiverFactionName,
    Guid? AntagonistFactionId,
    string? AntagonistFactionName,
    IReadOnlySet<Guid> AntagonistTargetEntityIds,
    IReadOnlyDictionary<string, IReadOnlyList<QuestChainHandoff>> HandoffsByUpstreamNodeId
);

public class QuestChainCastingGenerator(Random random)
{
    public const int FactionGiverMinimumNodeCount = 16;

    public QuestChainCast Cast(
        IReadOnlyList<QuestChainNodeSkeleton> skeleton,
        QuestChainGeneratorInput input
    )
    {
        var creatures = input
            .AvailableEntities.Where(entity =>
                entity.Type == QuestChainEntityTypes.Creature && entity.CanGiveQuests
            )
            .ToArray();
        if (creatures.Length == 0)
        {
            throw new InvalidOperationException("Quest-chain casting requires a creature giver.");
        }

        var eligibleFactionRosters = creatures
            .Where(entity =>
                entity.FactionId != null
                && entity.FactionKind
                    is FactionKind.Joinable
                        or FactionKind.CityGuard
                        or FactionKind.Castle
            )
            .GroupBy(entity => entity.FactionId!.Value)
            .Where(group =>
                input.FactionStandings.Any(standing =>
                    standing.FactionId == group.Key && standing.Score < 0
                )
            )
            .ToArray();
        var requiresFaction = skeleton.Count >= FactionGiverMinimumNodeCount;
        var useFaction = requiresFaction || (eligibleFactionRosters.Length > 0 && Next(2) == 1);

        QuestChainCandidateEntity[] giverRoster;
        Guid? giverFactionId = null;
        string? giverFactionName = null;
        if (useFaction)
        {
            if (eligibleFactionRosters.Length == 0)
            {
                throw new InvalidOperationException(
                    "A long quest chain requires an eligible faction giver roster."
                );
            }
            var factionRoster = eligibleFactionRosters[Next(eligibleFactionRosters.Length)];
            giverRoster = factionRoster.ToArray();
            giverFactionId = factionRoster.Key;
            giverFactionName = giverRoster[0].FactionName;
        }
        else
        {
            var giversWithAntagonists = creatures
                .Where(entity =>
                    entity.FactionId is { } factionId
                    && input.FactionStandings.Any(standing =>
                        standing.FactionId == factionId && standing.Score < 0
                    )
                )
                .ToArray();
            var singleGiverCandidates =
                giversWithAntagonists.Length > 0 ? giversWithAntagonists : creatures;
            giverRoster = [singleGiverCandidates[Next(singleGiverCandidates.Length)]];
        }

        var giverIdByNodeId = skeleton.ToDictionary(
            node => node.NodeId,
            _ => giverRoster[Next(giverRoster.Length)].Id,
            StringComparer.Ordinal
        );
        var antagonist = SelectAntagonist(input, giverFactionId, giverRoster[0].FactionId);
        var handoffs = BuildHandoffs(skeleton, giverIdByNodeId, creatures);

        return new QuestChainCast(
            giverIdByNodeId,
            giverFactionId,
            giverFactionName,
            antagonist?.FactionId,
            antagonist?.FactionName,
            antagonist?.TargetIds ?? new HashSet<Guid>(),
            handoffs
        );
    }

    private AntagonistSelection? SelectAntagonist(
        QuestChainGeneratorInput input,
        Guid? factionGiverId,
        Guid? singleGiverFactionId
    )
    {
        var giverFactionId = factionGiverId ?? singleGiverFactionId;
        if (giverFactionId == null)
        {
            return null;
        }

        var eligible = input
            .FactionStandings.Where(standing =>
                standing.FactionId == giverFactionId && standing.Score < 0
            )
            .Select(standing => new
            {
                Standing = standing,
                Targets = input
                    .AvailableEntities.Where(entity =>
                        entity.FactionId == standing.OtherFactionId
                        && entity.Type
                            is QuestChainEntityTypes.Creature
                                or QuestChainEntityTypes.Dungeon
                    )
                    .ToArray(),
            })
            .Where(candidate => candidate.Targets.Length > 0)
            .ToArray();
        var wildernessTargets = input
            .AvailableEntities.Where(entity =>
                entity.FactionId != null
                && entity.FactionKind == FactionKind.Wilderness
                && entity.Type == QuestChainEntityTypes.Creature
            )
            .GroupBy(entity => entity.FactionId!.Value)
            .ToArray();
        if (eligible.Length == 0 && wildernessTargets.Length == 0)
        {
            return null;
        }

        if (wildernessTargets.Length > 0 && (eligible.Length == 0 || Next(4) == 0))
        {
            var wilderness = wildernessTargets[Next(wildernessTargets.Length)].ToArray();
            return new AntagonistSelection(
                wilderness[0].FactionId!.Value,
                wilderness[0].FactionName,
                wilderness.Select(target => target.Id).ToHashSet()
            );
        }

        var totalWeight = eligible.Sum(candidate => -candidate.Standing.Score);
        var remaining = random.NextDouble() * totalWeight;
        var selected = eligible[^1];
        foreach (var candidate in eligible)
        {
            if (remaining < -candidate.Standing.Score)
            {
                selected = candidate;
                break;
            }
            remaining += candidate.Standing.Score;
        }

        return new AntagonistSelection(
            selected.Standing.OtherFactionId,
            selected.Targets[0].FactionName,
            selected.Targets.Select(target => target.Id).ToHashSet()
        );
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<QuestChainHandoff>> BuildHandoffs(
        IReadOnlyList<QuestChainNodeSkeleton> skeleton,
        IReadOnlyDictionary<string, Guid> giverIdByNodeId,
        IReadOnlyCollection<QuestChainCandidateEntity> creatures
    )
    {
        var namesById = creatures.ToDictionary(creature => creature.Id, creature => creature.Name);
        var handoffs = new Dictionary<string, List<QuestChainHandoff>>(StringComparer.Ordinal);
        foreach (var node in skeleton)
        {
            var prerequisiteGiverIds = node
                .PrerequisiteNodeIds.Select(id => giverIdByNodeId[id])
                .ToHashSet();
            var giverId = giverIdByNodeId[node.NodeId];
            if (node.PrerequisiteNodeIds.Count == 0 || prerequisiteGiverIds.Contains(giverId))
            {
                continue;
            }

            foreach (var prerequisiteNodeId in node.PrerequisiteNodeIds)
            {
                if (!handoffs.TryGetValue(prerequisiteNodeId, out var entries))
                {
                    entries = [];
                    handoffs[prerequisiteNodeId] = entries;
                }
                entries.Add(new QuestChainHandoff(prerequisiteNodeId, giverId, namesById[giverId]));
            }
        }

        return handoffs.ToDictionary(
            pair => pair.Key,
            pair =>
                (IReadOnlyList<QuestChainHandoff>)
                    pair.Value.DistinctBy(x => x.NextGiverId).ToArray(),
            StringComparer.Ordinal
        );
    }

    private int Next(int maximumExclusive) => (int)(random.NextDouble() * maximumExclusive);

    private record AntagonistSelection(
        Guid FactionId,
        string? FactionName,
        IReadOnlySet<Guid> TargetIds
    );
}
