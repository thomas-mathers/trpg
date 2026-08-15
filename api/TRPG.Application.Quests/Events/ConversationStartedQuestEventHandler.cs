using TRPG.Application.Common.Events;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Events;

public sealed class ConversationStartedQuestEventHandler(
    QuestObjectiveAdvancer questObjectiveAdvancer
) : IDomainEventConsumer<NpcConversationStartedEvent>
{
    public Task Handle(
        NpcConversationStartedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective is SpeakToCreatureObjective speak
                && speak.CreatureId == domainEvent.NpcId,
            cancellationToken
        );
}
