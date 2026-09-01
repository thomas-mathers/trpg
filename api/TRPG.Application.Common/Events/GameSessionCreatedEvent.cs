namespace TRPG.Application.Common.Events;

public sealed record GameSessionCreatedEvent(Guid SessionId, string SystemPrompt) : DomainEvent;
