using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class FactDisclosedQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
    : IDomainEventConsumer<NpcFactDisclosedEvent>
{
    public Task Handle(
        NpcFactDisclosedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective is LearnFactFromCreatureObjective learnFact
                && learnFact.CreatureId == domainEvent.NpcId
                && learnFact.FactId == domainEvent.FactId,
            cancellationToken
        );
}
