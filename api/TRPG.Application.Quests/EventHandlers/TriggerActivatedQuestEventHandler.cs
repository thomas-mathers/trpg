using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class TriggerActivatedQuestEventHandler(
    QuestObjectiveAdvancer questObjectiveAdvancer
) : IDomainEventConsumer<TriggerActivatedEvent>
{
    public async Task Handle(
        TriggerActivatedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        await questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective is InteractWithPropObjective interact
                && interact.TriggerId == domainEvent.TriggerId,
            cancellationToken
        );
}
