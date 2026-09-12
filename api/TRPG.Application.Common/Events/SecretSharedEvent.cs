namespace TRPG.Application.Common.Events;

public sealed record SecretSharedEvent(Guid PlayerId, Guid WorldId, Guid SecretId, Guid RecipientId)
    : DomainEvent;
