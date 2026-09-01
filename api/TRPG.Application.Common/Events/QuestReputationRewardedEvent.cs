using TRPG.Domain.Models;

namespace TRPG.Application.Common.Events;

public sealed record QuestReputationRewardedEvent(
    Guid PlayerId,
    Guid WorldId,
    IReadOnlyCollection<QuestReputationReward> Rewards,
    string Detail
) : DomainEvent;
