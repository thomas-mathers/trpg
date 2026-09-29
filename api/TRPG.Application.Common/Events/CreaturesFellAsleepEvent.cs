namespace TRPG.Application.Common.Events;

public sealed record CreaturesFellAsleepEvent(IReadOnlyCollection<Guid> CreatureIds) : DomainEvent;
