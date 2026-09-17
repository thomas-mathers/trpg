using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

// Completing any supporting quest for a fact raises the NPC's overall disposition, so it clears
// every lockout on that fact regardless of which approach failed — not just the one the completed
// quest happens to be listed under.
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
