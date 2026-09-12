using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class CreatureFreedQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
    : IDomainEventConsumer<CreatureFreedEvent>
{
    public Task Handle(
        CreatureFreedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective is FreeCreatureObjective free
                && free.CreatureId == domainEvent.CreatureId,
            cancellationToken
        );
}
