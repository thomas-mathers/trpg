using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Books.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Knowledge.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Quests.Results;
using TRPG.Application.Reputations.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests;

// Shared by AskAboutFactCommand/OfferBribeForFactCommand/IntimidateForFactCommand: each supplies
// its own approach-specific score contribution (0 for a plain ask), everything else about
// resolving a disclosure attempt is identical across the three.
internal sealed class FactDisclosureResolver(
    IQuestsDbContext context,
    IQueryHandler<
        GetActiveLearnFactObjectiveQuery,
        LearnFactFromCreatureObjective?
    > getActiveObjective,
    IQueryHandler<GetEffectiveReputationQuery, int> getEffectiveReputation,
    IQueryHandler<GetFactByIdQuery, Fact?> getFactById,
    ICommandHandler<LearnFactCommand, bool> learnFact,
    IDomainEventPublisher<NpcFactDisclosedEvent> factDisclosed
)
{
    internal async Task<FactDisclosureResult> Resolve(
        Guid worldId,
        Guid playerId,
        Guid npcId,
        Guid factId,
        FactDisclosureApproach? approach,
        Func<LearnFactFromCreatureObjective, int> computeApproachContribution,
        FactDisclosureOptions options,
        CancellationToken cancellationToken
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var objective =
            await getActiveObjective.Handle(
                new GetActiveLearnFactObjectiveQuery
                {
                    WorldId = worldId,
                    PlayerId = playerId,
                    NpcId = npcId,
                    FactId = factId,
                },
                cancellationToken
            ) ?? throw new EntityNotFoundException("Active learn-fact objective", npcId);

        if (
            await AnyIncomplete(
                objective.RequiredSupportingQuestIds,
                playerId,
                worldId,
                cancellationToken
            )
        )
        {
            transaction.Complete();
            return new FactDisclosureResult(FactDisclosureOutcome.Blocked);
        }

        if (
            approach is { } lockableApproach
            && await IsLockedOut(
                worldId,
                playerId,
                npcId,
                factId,
                lockableApproach,
                cancellationToken
            )
        )
        {
            transaction.Complete();
            return new FactDisclosureResult(FactDisclosureOutcome.LockedOut);
        }

        var completedWeightedTotal = await CompletedWeightedTotal(
            objective.WeightedSupportingQuestIds,
            playerId,
            worldId,
            cancellationToken
        );

        var effectiveReputation = await getEffectiveReputation.Handle(
            new GetEffectiveReputationQuery
            {
                ObserverCreatureId = playerId,
                TargetCreatureId = npcId,
            },
            cancellationToken
        );

        var score = FactDisclosureScoreCalculator.Score(
            objective.BaseWillingness,
            effectiveReputation,
            computeApproachContribution(objective),
            completedWeightedTotal,
            options
        );

        if (!FactDisclosureScoreCalculator.Succeeds(score, options))
        {
            if (approach is { } failedApproach)
            {
                context.FactDisclosureLockouts.Add(
                    new FactDisclosureLockout
                    {
                        WorldId = worldId,
                        PlayerId = playerId,
                        NpcId = npcId,
                        FactId = factId,
                        Approach = failedApproach,
                    }
                );
                await context.SaveChangesAsync(cancellationToken);
            }

            transaction.Complete();
            return new FactDisclosureResult(FactDisclosureOutcome.Failed);
        }

        await learnFact.Handle(
            new LearnFactCommand
            {
                WorldId = worldId,
                KnowerId = playerId,
                FactId = factId,
            },
            cancellationToken
        );
        await factDisclosed.Publish(
            new NpcFactDisclosedEvent(playerId, worldId, npcId, factId),
            cancellationToken
        );

        var fact = await getFactById.Handle(
            new GetFactByIdQuery { FactId = factId },
            cancellationToken
        );

        transaction.Complete();
        return new FactDisclosureResult(FactDisclosureOutcome.Disclosed, fact?.Value);
    }

    private async Task<bool> AnyIncomplete(
        IReadOnlyCollection<Guid> questIds,
        Guid playerId,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        if (questIds.Count == 0)
        {
            return false;
        }

        var completedCount = await context
            .CreatureQuests.Where(creatureQuest =>
                creatureQuest.CreatureId == playerId
                && creatureQuest.WorldId == worldId
                && creatureQuest.Status == QuestStatus.Completed
                && questIds.AsEnumerable().Contains(creatureQuest.QuestId)
            )
            .Select(creatureQuest => creatureQuest.QuestId)
            .Distinct()
            .CountAsync(cancellationToken);

        return completedCount < questIds.Count;
    }

    private async Task<int> CompletedWeightedTotal(
        IReadOnlyCollection<SupportingFactQuestWeight> weighted,
        Guid playerId,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        if (weighted.Count == 0)
        {
            return 0;
        }

        var questIds = weighted.Select(w => w.QuestId).ToArray();
        var completedQuestIds = await context
            .CreatureQuests.Where(creatureQuest =>
                creatureQuest.CreatureId == playerId
                && creatureQuest.WorldId == worldId
                && creatureQuest.Status == QuestStatus.Completed
                && questIds.AsEnumerable().Contains(creatureQuest.QuestId)
            )
            .Select(creatureQuest => creatureQuest.QuestId)
            .ToArrayAsync(cancellationToken);

        var completedSet = completedQuestIds.ToHashSet();
        return weighted.Where(w => completedSet.Contains(w.QuestId)).Sum(w => w.Weight);
    }

    private async Task<bool> IsLockedOut(
        Guid worldId,
        Guid playerId,
        Guid npcId,
        Guid factId,
        FactDisclosureApproach approach,
        CancellationToken cancellationToken
    ) =>
        await context.FactDisclosureLockouts.AnyAsync(
            lockout =>
                lockout.WorldId == worldId
                && lockout.PlayerId == playerId
                && lockout.NpcId == npcId
                && lockout.FactId == factId
                && lockout.Approach == approach,
            cancellationToken
        );
}
