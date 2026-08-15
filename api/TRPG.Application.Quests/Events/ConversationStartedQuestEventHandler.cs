using TRPG.Data.Models;

namespace TRPG.Application.Quests.Events;

public sealed record ConversationStartedQuestEvent(Guid PlayerId, Guid WorldId, Guid CreatureId);

public sealed class ConversationStartedQuestEventHandler(
    QuestObjectiveAdvancer questObjectiveAdvancer
)
{
    public Task Handle(
        ConversationStartedQuestEvent questEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            questEvent.PlayerId,
            questEvent.WorldId,
            objective =>
                objective is SpeakToCreatureObjective speak
                && speak.CreatureId == questEvent.CreatureId,
            cancellationToken
        );
}
