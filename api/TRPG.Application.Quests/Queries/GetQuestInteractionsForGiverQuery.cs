using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Knowledge.Queries;
using TRPG.Application.Quests.Results;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public class GetQuestInteractionsForGiverQuery
{
    public required Guid GiverId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GetQuestInteractionsForGiverQueryHandler(
    IQueryHandler<GetKnownFactIdsQuery, IReadOnlyList<Guid>> getKnownFacts,
    IQuestsDbContext context,
    IQueryHandler<GetItemNamesByIdsQuery, IReadOnlyDictionary<Guid, string>> getItemNamesByIds
) : IQueryHandler<GetQuestInteractionsForGiverQuery, QuestInteractionsResult>
{
    public async Task<QuestInteractionsResult> Handle(
        GetQuestInteractionsForGiverQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var giverQuests = await context
            .Quests.AsNoTracking()
            .Where(quest => quest.GiverId == query.GiverId && quest.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);

        var giverQuestIds = giverQuests.Select(quest => quest.Id).ToHashSet();

        var playerQuests = await context
            .CreatureQuests.AsNoTracking()
            .Where(creatureQuest =>
                creatureQuest.CreatureId == query.PlayerId && creatureQuest.WorldId == query.WorldId
            )
            .ToArrayAsync(cancellationToken);

        var completedQuestIds = playerQuests
            .Where(creatureQuest => creatureQuest.Status == QuestStatus.Completed)
            .Select(creatureQuest => creatureQuest.QuestId)
            .ToHashSet();

        var objectives = await context
            .QuestObjectives.AsNoTracking()
            .Where(objective => giverQuestIds.AsEnumerable().Contains(objective.QuestId))
            .ToArrayAsync(cancellationToken);

        var itemIds = objectives
            .OfType<GiveItemsObjective>()
            .SelectMany(objective => objective.ItemIds)
            .Distinct()
            .ToArray();
        var itemNamesById =
            itemIds.Length == 0
                ? new Dictionary<Guid, string>()
                : await getItemNamesByIds.Handle(
                    new GetItemNamesByIdsQuery { WorldId = query.WorldId, ItemIds = itemIds },
                    cancellationToken
                );

        var objectivesByQuestId = objectives
            .GroupBy(objective => objective.QuestId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(objective => ToResult(objective, itemNamesById)).ToArray()
            );

        var giverPlayerQuests = playerQuests
            .Where(creatureQuest => giverQuestIds.Contains(creatureQuest.QuestId))
            .ToArray();

        var acceptedQuestIds = giverPlayerQuests.Select(quest => quest.QuestId).ToHashSet();

        var knownFacts = await getKnownFacts.Handle(
            new GetKnownFactIdsQuery(query.WorldId, query.PlayerId),
            cancellationToken
        );

        var availableQuests = giverQuests
            .Where(quest => !acceptedQuestIds.Contains(quest.Id))
            .Where(quest =>
                quest.RequiredFactId == null || knownFacts.Contains(quest.RequiredFactId.Value)
            )
            .Where(quest => quest.PrerequisiteQuestIds.All(completedQuestIds.Contains))
            .Select(quest => ToResult(quest, objectivesByQuestId))
            .ToArray();

        var questsById = giverQuests.ToDictionary(quest => quest.Id);

        var activeQuests = giverPlayerQuests
            .Where(quest => quest.Status == QuestStatus.Accepted)
            .Select(quest => questsById[quest.QuestId])
            .Select(quest => ToResult(quest, objectivesByQuestId))
            .ToArray();

        var readyToCompleteQuests = giverPlayerQuests
            .Where(quest => quest.Status == QuestStatus.ReadyToComplete)
            .Select(quest => questsById[quest.QuestId])
            .Select(quest => ToResult(quest, objectivesByQuestId))
            .ToArray();

        var completedQuests = giverPlayerQuests
            .Where(quest => quest.Status == QuestStatus.Completed)
            .Select(quest => questsById[quest.QuestId])
            .Select(quest => ToResult(quest, objectivesByQuestId))
            .ToArray();

        return new QuestInteractionsResult(
            availableQuests,
            activeQuests,
            readyToCompleteQuests,
            completedQuests
        );
    }

    private static QuestConversationResult ToResult(
        Quest quest,
        IReadOnlyDictionary<Guid, QuestConversationObjectiveResult[]> objectivesByQuestId
    ) =>
        new(
            quest.Id,
            quest.Name,
            quest.Description,
            quest.GoldReward,
            objectivesByQuestId.GetValueOrDefault(quest.Id, [])
        );

    private static QuestConversationObjectiveResult ToResult(
        QuestObjective objective,
        IReadOnlyDictionary<Guid, string> itemNamesById
    )
    {
        // Only worth breaking down when the objective actually spans different kinds of item — a
        // single type repeated (e.g. two Goblin Ear drops) already says its count in Description.
        var distinctItemNames = objective is GiveItemsObjective giveItems
            ? giveItems
                .ItemIds.Select(itemId => itemNamesById.GetValueOrDefault(itemId, "Unknown Item"))
                .Distinct()
                .ToArray()
            : null;

        return new QuestConversationObjectiveResult(
            objective.Name,
            objective.Description,
            objective.RequiredAmount,
            distinctItemNames is { Length: > 1 } ? distinctItemNames : null
        );
    }
}
