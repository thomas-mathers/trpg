using TRPG.Application.Common.Events;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Events;

internal sealed class CreatureKilledQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
    : IDomainEventConsumer<CreatureKilledEvent>
{
    public Task Handle(
        CreatureKilledEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective switch
                {
                    KillCreatureObjective kill => kill.CreatureId == domainEvent.CreatureId,
                    KillCreatureTypeObjective kill => kill.CreatureType == domainEvent.CreatureType,
                    _ => false,
                },
            cancellationToken
        );
}
