using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Quests.Results;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public class GetQuestInteractionsForGiverQuery
{
    public required Guid GiverId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GetQuestInteractionsForGiverQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetQuestInteractionsForGiverQuery, QuestInteractionsResult>
{
    public async Task<QuestInteractionsResult> Handle(
        GetQuestInteractionsForGiverQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var quests = await context
            .Quests.AsNoTracking()
            .Where(quest => quest.GiverId == query.GiverId && quest.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);

        var questIds = quests.Select(quest => quest.Id).ToArray();

        var creatureQuests = await context
            .CreatureQuests.AsNoTracking()
            .Where(creatureQuest =>
                creatureQuest.CreatureId == query.PlayerId
                && creatureQuest.WorldId == query.WorldId
                && questIds.AsEnumerable().Contains(creatureQuest.QuestId)
            )
            .ToArrayAsync(cancellationToken);

        var completedQuestIds = await context
            .CreatureQuests.AsNoTracking()
            .Where(creatureQuest =>
                creatureQuest.CreatureId == query.PlayerId
                && creatureQuest.WorldId == query.WorldId
                && creatureQuest.Status == QuestStatus.Completed
            )
            .Select(creatureQuest => creatureQuest.QuestId)
            .ToArrayAsync(cancellationToken);

        var objectives = await context
            .QuestObjectives.AsNoTracking()
            .Where(objective => questIds.AsEnumerable().Contains(objective.QuestId))
            .ToArrayAsync(cancellationToken);

        var objectivesByQuestId = objectives
            .GroupBy(objective => objective.QuestId)
            .ToDictionary(group => group.Key, group => group.Select(ToResult).ToArray());

        var acceptedQuestIds = creatureQuests.Select(quest => quest.QuestId).ToHashSet();

        var completedQuestIdSet = completedQuestIds.ToHashSet();

        var availableQuests = quests
            .Where(quest => !acceptedQuestIds.Contains(quest.Id))
            .Where(quest => quest.PrerequisiteQuestIds.All(completedQuestIdSet.Contains))
            .Select(quest => ToResult(quest, objectivesByQuestId))
            .ToArray();

        var questsById = quests.ToDictionary(quest => quest.Id);

        var readyToCompleteQuests = creatureQuests
            .Where(quest => quest.Status == QuestStatus.ReadyToComplete)
            .Select(quest => questsById[quest.QuestId])
            .Select(quest => ToResult(quest, objectivesByQuestId))
            .ToArray();

        return new QuestInteractionsResult(availableQuests, readyToCompleteQuests);
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

    private static QuestConversationObjectiveResult ToResult(QuestObjective objective) =>
        new(objective.Name, objective.Description, objective.RequiredAmount);
}
