using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

// Supporting-quest progress can make every disclosure approach viable again.
internal sealed class FactDisclosureLockoutResetEventHandler(IQuestsDbContext context)
    : IDomainEventConsumer<QuestCompletedEvent>
{
    public async Task Handle(
        QuestCompletedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var candidates = await context
            .QuestObjectives.OfType<LearnFactFromCreatureObjective>()
            .Where(objective => objective.WorldId == domainEvent.WorldId)
            .ToArrayAsync(cancellationToken);

        var affectedNpcAndFactPairs = candidates
            .Where(objective =>
                objective.RequiredSupportingQuestIds.Contains(domainEvent.QuestId)
                || objective.WeightedSupportingQuestIds.Any(weighted =>
                    weighted.QuestId == domainEvent.QuestId
                )
            )
            .Select(objective => (objective.CreatureId, objective.FactId))
            .Distinct()
            .ToArray();

        foreach (var (npcId, factId) in affectedNpcAndFactPairs)
        {
            await context
                .FactDisclosureLockouts.Where(lockout =>
                    lockout.WorldId == domainEvent.WorldId
                    && lockout.PlayerId == domainEvent.PlayerId
                    && lockout.NpcId == npcId
                    && lockout.FactId == factId
                )
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
