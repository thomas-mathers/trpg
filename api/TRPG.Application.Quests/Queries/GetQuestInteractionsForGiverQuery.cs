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

        var objectivesByQuestId = objectives
            .GroupBy(objective => objective.QuestId)
            .ToDictionary(group => group.Key, group => group.Select(ToResult).ToArray());

        var giverPlayerQuests = playerQuests
            .Where(creatureQuest => giverQuestIds.Contains(creatureQuest.QuestId))
            .ToArray();

        var acceptedQuestIds = giverPlayerQuests.Select(quest => quest.QuestId).ToHashSet();

        var availableQuests = giverQuests
            .Where(quest => !acceptedQuestIds.Contains(quest.Id))
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

    private static QuestConversationObjectiveResult ToResult(QuestObjective objective) =>
        new(objective.Name, objective.Description, objective.RequiredAmount);
}
