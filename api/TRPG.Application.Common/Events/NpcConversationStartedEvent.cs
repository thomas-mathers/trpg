namespace TRPG.Application.Common.Events;

public sealed record NpcConversationStartedEvent(Guid PlayerId, Guid WorldId, Guid NpcId)
    : DomainEvent;
