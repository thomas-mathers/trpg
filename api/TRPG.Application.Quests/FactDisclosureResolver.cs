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

internal record FactDisclosureRequest(
    Guid WorldId,
    Guid PlayerId,
    Guid NpcId,
    Guid FactId,
    FactDisclosureApproach? Approach
);

internal record FactDisclosureAssessment(int Contribution, FactDisclosureOutcome? Rejection = null);

internal sealed class FactDisclosureResolver(
    IQuestsDbContext context,
    IQueryHandler<
        GetActiveLearnFactObjectiveQuery,
        LearnFactFromCreatureObjective?
    > getActiveObjective,
    IQueryHandler<GetEffectiveReputationQuery, int> getEffectiveReputation,
    IQueryHandler<GetFactByIdQuery, Fact?> getFactById,
    ICommandHandler<LearnFactCommand, bool> learnFact,
    IDomainEventPublisher<NpcFactDisclosedEvent> factDisclosed,
    FactDisclosureAttemptRecorder attempts
)
{
    internal async Task<FactDisclosureResult> Resolve(
        FactDisclosureRequest request,
        Func<LearnFactFromCreatureObjective, FactDisclosureAssessment> assess,
        FactDisclosureOptions options,
        CancellationToken cancellationToken
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );
        var objective = await GetObjective(request, cancellationToken);
        var outcome = await Evaluate(
            request,
            objective,
            assess(objective),
            options,
            cancellationToken
        );
        var result = outcome switch
        {
            FactDisclosureOutcome.Disclosed => await Disclose(request, cancellationToken),
            FactDisclosureOutcome.LockedOut => new FactDisclosureResult(outcome),
            _ => await Reject(request, objective, outcome, cancellationToken),
        };
        transaction.Complete();
        return result;
    }

    private async Task<LearnFactFromCreatureObjective> GetObjective(
        FactDisclosureRequest request,
        CancellationToken cancellationToken
    )
    {
        return await getActiveObjective.Handle(
                new GetActiveLearnFactObjectiveQuery
                {
                    WorldId = request.WorldId,
                    PlayerId = request.PlayerId,
                    NpcId = request.NpcId,
                    FactId = request.FactId,
                },
                cancellationToken
            ) ?? throw new EntityNotFoundException("Active learn-fact objective", request.NpcId);
    }

    private async Task<FactDisclosureOutcome> Evaluate(
        FactDisclosureRequest request,
        LearnFactFromCreatureObjective objective,
        FactDisclosureAssessment assessment,
        FactDisclosureOptions options,
        CancellationToken cancellationToken
    )
    {
        if (
            request.Approach is { } approach
            && await IsLockedOut(request, approach, cancellationToken)
        )
            return FactDisclosureOutcome.LockedOut;
        if (assessment.Rejection is { } rejection)
            return rejection;
        var missing = await GetIncompleteQuestIds(
            objective.RequiredSupportingQuestIds,
            request.PlayerId,
            request.WorldId,
            cancellationToken
        );
        if (missing.Length > 0)
            return FactDisclosureOutcome.Blocked;
        var score = await GetScore(
            request,
            objective,
            assessment.Contribution,
            options,
            cancellationToken
        );
        return FactDisclosureScoreCalculator.Succeeds(score, options)
            ? FactDisclosureOutcome.Disclosed
            : FactDisclosureOutcome.Failed;
    }

    private async Task<int> GetScore(
        FactDisclosureRequest request,
        LearnFactFromCreatureObjective objective,
        int contribution,
        FactDisclosureOptions options,
        CancellationToken cancellationToken
    )
    {
        var completedWeight = await CompletedWeightedTotal(
            objective.WeightedSupportingQuestIds,
            request.PlayerId,
            request.WorldId,
            cancellationToken
        );
        var reputation = await getEffectiveReputation.Handle(
            new GetEffectiveReputationQuery
            {
                ObserverCreatureId = request.PlayerId,
                TargetCreatureId = request.NpcId,
            },
            cancellationToken
        );
        return FactDisclosureScoreCalculator.Score(
            objective.BaseWillingness,
            reputation,
            contribution,
            completedWeight,
            options
        );
    }

    private async Task<FactDisclosureResult> Reject(
        FactDisclosureRequest request,
        LearnFactFromCreatureObjective objective,
        FactDisclosureOutcome outcome,
        CancellationToken cancellationToken
    )
    {
        if (outcome == FactDisclosureOutcome.Failed && request.Approach is { } approach)
        {
            context.FactDisclosureLockouts.Add(
                new FactDisclosureLockout
                {
                    WorldId = request.WorldId,
                    PlayerId = request.PlayerId,
                    NpcId = request.NpcId,
                    FactId = request.FactId,
                    Approach = approach,
                }
            );
            await context.SaveChangesAsync(cancellationToken);
        }
        var reason = await attempts.Record(request, objective.ReasonFactId, cancellationToken);
        return new FactDisclosureResult(outcome, ReasonFact: reason);
    }

    private async Task<FactDisclosureResult> Disclose(
        FactDisclosureRequest request,
        CancellationToken cancellationToken
    )
    {
        var fact =
            await getFactById.Handle(
                new GetFactByIdQuery { FactId = request.FactId },
                cancellationToken
            ) ?? throw new EntityNotFoundException("Fact", request.FactId);
        await learnFact.Handle(
            new LearnFactCommand
            {
                WorldId = request.WorldId,
                KnowerId = request.PlayerId,
                FactId = request.FactId,
            },
            cancellationToken
        );
        await factDisclosed.Publish(
            new NpcFactDisclosedEvent(
                request.PlayerId,
                request.WorldId,
                request.NpcId,
                request.FactId
            ),
            cancellationToken
        );
        return new FactDisclosureResult(FactDisclosureOutcome.Disclosed, fact.Value);
    }

    private async Task<Guid[]> GetIncompleteQuestIds(
        IReadOnlyCollection<Guid> questIds,
        Guid playerId,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        if (questIds.Count == 0)
        {
            return [];
        }

        var completedQuestIds = await context
            .CreatureQuests.Where(creatureQuest =>
                creatureQuest.CreatureId == playerId
                && creatureQuest.WorldId == worldId
                && creatureQuest.Status == QuestStatus.Completed
                && questIds.AsEnumerable().Contains(creatureQuest.QuestId)
            )
            .Select(creatureQuest => creatureQuest.QuestId)
            .ToArrayAsync(cancellationToken);

        return questIds.Except(completedQuestIds).ToArray();
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
        FactDisclosureRequest request,
        FactDisclosureApproach approach,
        CancellationToken cancellationToken
    ) =>
        await context.FactDisclosureLockouts.AnyAsync(
            lockout =>
                lockout.WorldId == request.WorldId
                && lockout.PlayerId == request.PlayerId
                && lockout.NpcId == request.NpcId
                && lockout.FactId == request.FactId
                && lockout.Approach == approach,
            cancellationToken
        );
}
