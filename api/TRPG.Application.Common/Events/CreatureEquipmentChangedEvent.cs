namespace TRPG.Application.Common.Events;

public sealed record CreatureEquipmentChangedEvent(Guid CreatureId) : DomainEvent;
