namespace TRPG.Application.Common.Events;

public sealed record GameSessionDeletedEvent(Guid SessionId) : DomainEvent;
