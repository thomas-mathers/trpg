namespace TRPG.Application.Common.Events;

public record FactLearnedEvent(Guid WorldId, Guid KnowerId, Guid FactId) : DomainEvent;
