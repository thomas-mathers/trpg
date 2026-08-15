using TRPG.Data.Models;

namespace TRPG.Application.Quests.Events;

public sealed record PlayerMovedQuestEvent(Guid PlayerId, Guid WorldId, Guid LocationId);

public sealed class PlayerMovedQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
{
    public Task Handle(
        PlayerMovedQuestEvent questEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            questEvent.PlayerId,
            questEvent.WorldId,
            objective =>
                objective is ExploreLocationObjective explore
                && explore.LocationId == questEvent.LocationId,
            cancellationToken
        );
}
