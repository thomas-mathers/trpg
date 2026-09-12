using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class SecretSharedQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
    : IDomainEventConsumer<SecretSharedEvent>
{
    public Task Handle(
        SecretSharedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective is ShareSecretObjective share
                && share.SecretId == domainEvent.SecretId
                && share.RecipientId == domainEvent.RecipientId,
            cancellationToken
        );
}
