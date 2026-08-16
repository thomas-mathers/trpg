using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public class GetQuestInteractionsForGiverQuery
{
    public required Guid GiverId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

public record QuestConversationObjective(string Name, string Description, int RequiredAmount);

public record QuestConversationDetail(
    [property: JsonIgnore] Guid QuestId,
    string Name,
    string Description,
    int GoldReward,
    IReadOnlyCollection<QuestConversationObjective> Objectives
);

public record QuestInteractionsForGiver(
    IReadOnlyCollection<QuestConversationDetail> AvailableQuests,
    IReadOnlyCollection<QuestConversationDetail> ReadyToCompleteQuests
);

internal class GetQuestInteractionsForGiverQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetQuestInteractionsForGiverQuery, QuestInteractionsForGiver>
{
    public async Task<QuestInteractionsForGiver> Handle(
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
            .ToDictionary(group => group.Key, group => group.Select(ToDetail).ToArray());

        var acceptedQuestIds = creatureQuests.Select(quest => quest.QuestId).ToHashSet();

        var completedQuestIdSet = completedQuestIds.ToHashSet();

        var availableQuests = quests
            .Where(quest => !acceptedQuestIds.Contains(quest.Id))
            .Where(quest => quest.PrerequisiteQuestIds.All(completedQuestIdSet.Contains))
            .Select(quest => ToDetail(quest, objectivesByQuestId))
            .ToArray();

        var questsById = quests.ToDictionary(quest => quest.Id);

        var readyToCompleteQuests = creatureQuests
            .Where(quest => quest.Status == QuestStatus.ReadyToComplete)
            .Select(quest => questsById[quest.QuestId])
            .Select(quest => ToDetail(quest, objectivesByQuestId))
            .ToArray();

        return new QuestInteractionsForGiver(availableQuests, readyToCompleteQuests);
    }

    private static QuestConversationDetail ToDetail(
        Quest quest,
        IReadOnlyDictionary<Guid, QuestConversationObjective[]> objectivesByQuestId
    ) =>
        new(
            quest.Id,
            quest.Name,
            quest.Description,
            quest.GoldReward,
            objectivesByQuestId.GetValueOrDefault(quest.Id, [])
        );

    private static QuestConversationObjective ToDetail(QuestObjective objective) =>
        new(objective.Name, objective.Description, objective.RequiredAmount);
}
