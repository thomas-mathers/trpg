namespace TRPG.Application.Common.Events;

public sealed record ItemGivenToCreatureEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid ItemId,
    Guid RecipientId
) : DomainEvent;
