using TRPG.Data.Models;

namespace TRPG.Application.Quests;

internal sealed record ConversationStartedQuestEvent(Guid PlayerId, Guid WorldId, Guid CreatureId);

internal sealed class ConversationStartedQuestEventHandler(
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
