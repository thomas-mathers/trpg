using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Quests.Commands;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class QuestCompletedExclusiveGroupEventHandler(
    ICommandHandler<FailExclusiveGroupSiblingsCommand> failExclusiveGroupSiblings
) : IDomainEventConsumer<QuestCompletedEvent>
{
    public Task Handle(
        QuestCompletedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        failExclusiveGroupSiblings.Handle(
            new FailExclusiveGroupSiblingsCommand
            {
                PlayerId = domainEvent.PlayerId,
                WorldId = domainEvent.WorldId,
                CompletedQuestId = domainEvent.QuestId,
            },
            cancellationToken
        );
}
