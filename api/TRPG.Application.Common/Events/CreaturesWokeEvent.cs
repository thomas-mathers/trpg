namespace TRPG.Application.Common.Events;

public sealed record CreaturesWokeEvent(IReadOnlyCollection<Guid> CreatureIds) : DomainEvent;
