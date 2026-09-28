namespace TRPG.Application.Common.Events;

public sealed record CreaturesDiedEvent(IReadOnlyCollection<Guid> CreatureIds) : DomainEvent;
