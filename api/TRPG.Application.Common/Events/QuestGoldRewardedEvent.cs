namespace TRPG.Application.Common.Events;

public sealed record QuestGoldRewardedEvent(Guid PlayerId, Guid WorldId, int Amount) : DomainEvent;
