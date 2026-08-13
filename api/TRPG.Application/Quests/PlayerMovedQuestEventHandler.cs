using TRPG.Data.Models;

namespace TRPG.Application.Quests;

internal sealed record PlayerMovedQuestEvent(Guid PlayerId, Guid WorldId, Guid LocationId);

internal sealed class PlayerMovedQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
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
