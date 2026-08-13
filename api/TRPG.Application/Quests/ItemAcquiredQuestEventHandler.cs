using TRPG.Data.Models;

namespace TRPG.Application.Quests;

internal sealed record ItemAcquiredQuestEvent(Guid PlayerId, Guid WorldId, Guid ItemId);

internal sealed class ItemAcquiredQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
{
    public Task Handle(
        ItemAcquiredQuestEvent questEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            questEvent.PlayerId,
            questEvent.WorldId,
            objective =>
                objective is CollectItemObjective collect && collect.ItemId == questEvent.ItemId,
            cancellationToken
        );
}
