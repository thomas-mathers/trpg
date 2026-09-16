using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

// Mirrors GetActiveClearLocationObjectiveBuildingIdsQuery's per-player, only-while-open exclusion,
// keyed by the target creature instead of a building.
public class GetActiveKillCreatureObjectiveCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class GetActiveKillCreatureObjectiveCreatureIdsQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetActiveKillCreatureObjectiveCreatureIdsQuery, IReadOnlySet<Guid>>
{
    private static readonly QuestStatus[] ActiveStatuses =
    [
        QuestStatus.Accepted,
        QuestStatus.ReadyToComplete,
    ];

    public async Task<IReadOnlySet<Guid>> Handle(
        GetActiveKillCreatureObjectiveCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var activeQuestIds = await context
            .CreatureQuests.AsNoTracking()
            .Where(quest =>
                quest.CreatureId == query.PlayerId
                && quest.WorldId == query.WorldId
                && ActiveStatuses.AsEnumerable().Contains(quest.Status)
            )
            .Select(quest => quest.QuestId)
            .ToArrayAsync(cancellationToken);

        if (activeQuestIds.Length == 0)
        {
            return new HashSet<Guid>();
        }

        var creatureIds = await context
            .QuestObjectives.AsNoTracking()
            .OfType<KillCreatureObjective>()
            .Where(objective =>
                objective.WorldId == query.WorldId
                && activeQuestIds.AsEnumerable().Contains(objective.QuestId)
            )
            .Select(objective => objective.CreatureId)
            .ToArrayAsync(cancellationToken);

        return creatureIds.ToHashSet();
    }
}
