using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Queries;

internal class GetQuestJournalQuery
{
    public required Guid PlayerId { get; init; }
}

internal record QuestObjectiveProgress(
    string Name,
    string Description,
    int Amount,
    int RequiredAmount
);

internal record QuestJournalEntry(
    Guid Id,
    string Name,
    string Description,
    int GoldReward,
    QuestStatus Status,
    bool IsTracked,
    IReadOnlyCollection<QuestObjectiveProgress> Objectives
);

internal class GetQuestJournalQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<QuestJournalEntry>> Handle(
        GetQuestJournalQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var playerWorldId = await GetPlayerWorldId(query.PlayerId, cancellationToken);
        var quests = await context
            .CreatureQuests.AsNoTracking()
            .Include(creatureQuest => creatureQuest.Quest)
            .Where(creatureQuest =>
                creatureQuest.CreatureId == query.PlayerId && creatureQuest.WorldId == playerWorldId
            )
            .OrderBy(creatureQuest => creatureQuest.Status)
            .ThenBy(creatureQuest => creatureQuest.Quest.Name)
            .ToArrayAsync(cancellationToken);
        var objectives = await GetObjectives(query.PlayerId, playerWorldId, cancellationToken);
        var objectivesByQuestId = objectives
            .GroupBy(objective => objective.Objective.QuestId)
            .ToDictionary(group => group.Key, group => group.Select(ToProgress).ToArray());

        return quests
            .Select(quest => new QuestJournalEntry(
                quest.QuestId,
                quest.Quest.Name,
                quest.Quest.Description,
                quest.Quest.GoldReward,
                quest.Status,
                quest.IsTracked,
                objectivesByQuestId.GetValueOrDefault(quest.QuestId, [])
            ))
            .ToArray();
    }

    private async Task<Guid> GetPlayerWorldId(Guid playerId, CancellationToken cancellationToken) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == playerId)
            .Select(creature => (Guid?)creature.WorldId)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new EntityNotFoundException("Player", playerId);

    private Task<CreatureQuestObjective[]> GetObjectives(
        Guid playerId,
        Guid worldId,
        CancellationToken cancellationToken
    ) =>
        context
            .CreatureQuestObjectives.AsNoTracking()
            .Include(objective => objective.Objective)
            .Where(objective => objective.CreatureId == playerId && objective.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

    private static QuestObjectiveProgress ToProgress(CreatureQuestObjective objective) =>
        new(
            objective.Objective.Name,
            objective.Objective.Description,
            objective.Amount,
            objective.Objective switch
            {
                KillCreatureTypeObjective kill => kill.RequiredAmount,
                CollectItemObjective collect => collect.RequiredAmount,
                _ => 1,
            }
        );
}
