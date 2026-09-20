using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class FactLearnedReportQuestEventHandler(
    QuestObjectiveAdvancer questObjectiveAdvancer
) : IDomainEventConsumer<FactLearnedEvent>
{
    public Task Handle(
        FactLearnedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            domainEvent.KnowerId,
            domainEvent.WorldId,
            objective =>
                objective is ReportFactToCreatureObjective report
                && report.FactId == domainEvent.FactId,
            cancellationToken
        );
}
