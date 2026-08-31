using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Reputations.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.EventHandlers;

internal sealed class QuestReputationRewardedEventHandler(
    ICommandHandler<AdjustReputationsCommand> adjustReputations
) : IDomainEventConsumer<QuestReputationRewardedEvent>
{
    public async Task Handle(
        QuestReputationRewardedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var rewardGroups = domainEvent.Rewards.GroupBy(reward => reward.TargetType);

        foreach (var group in rewardGroups)
        {
            await adjustReputations.Handle(
                new AdjustReputationsCommand
                {
                    CreatureId = domainEvent.PlayerId,
                    Adjustments = group
                        .Select(reward => new ReputationAdjustment(reward.TargetId, reward.Score))
                        .ToArray(),
                    TargetType = group.Key,
                    Reason = ReputationReason.QuestCompleted,
                    Detail = domainEvent.Detail,
                },
                cancellationToken
            );
        }
    }
}
