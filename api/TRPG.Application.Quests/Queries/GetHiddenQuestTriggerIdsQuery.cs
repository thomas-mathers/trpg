using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

// A trigger minted for an InteractWithProp objective belongs to that quest, so it stays out of
// sight until the player accepts it. Triggers no quest owns, like dungeon levers, are never hidden.
public class GetHiddenQuestTriggerIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required IReadOnlyCollection<Guid> TriggerIds { get; init; }
}

internal class GetHiddenQuestTriggerIdsQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetHiddenQuestTriggerIdsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetHiddenQuestTriggerIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.TriggerIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ownedTriggers = await context
            .QuestObjectives.AsNoTracking()
            .OfType<InteractWithPropObjective>()
            .Where(objective =>
                objective.WorldId == query.WorldId
                && query.TriggerIds.AsEnumerable().Contains(objective.TriggerId)
            )
            .Select(objective => new OwnedTrigger(objective.TriggerId, objective.QuestId))
            .ToArrayAsync(cancellationToken);

        var owningQuestIds = ownedTriggers.Select(owned => owned.QuestId).Distinct().ToArray();
        var acceptedQuestIds = await context
            .CreatureQuests.AsNoTracking()
            .Where(quest =>
                quest.CreatureId == query.PlayerId
                && quest.WorldId == query.WorldId
                && quest.Status == QuestStatus.Accepted
                && owningQuestIds.AsEnumerable().Contains(quest.QuestId)
            )
            .Select(quest => quest.QuestId)
            .ToArrayAsync(cancellationToken);

        return ownedTriggers
            .Where(owned => !acceptedQuestIds.Contains(owned.QuestId))
            .Select(owned => owned.TriggerId)
            .ToHashSet();
    }

    private record OwnedTrigger(Guid TriggerId, Guid QuestId);
}
