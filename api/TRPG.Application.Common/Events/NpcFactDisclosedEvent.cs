namespace TRPG.Application.Common.Events;

public sealed record NpcFactDisclosedEvent(Guid PlayerId, Guid WorldId, Guid NpcId, Guid FactId)
    : DomainEvent;
