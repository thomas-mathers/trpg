using TRPG.Application.Common.Events;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Events;

internal sealed class PlayerMovedQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
    : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective is ExploreLocationObjective explore
                && explore.LocationId == domainEvent.LocationId,
            cancellationToken
        );
}
