using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public class GetQuestJournalQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

public record QuestObjectiveProgress(
    string Name,
    string Description,
    int Amount,
    int RequiredAmount,
    string? LocationName
);

public record QuestJournalEntry(
    Guid Id,
    string Name,
    string Description,
    int GoldReward,
    QuestStatus Status,
    bool IsTracked,
    IReadOnlyCollection<QuestObjectiveProgress> Objectives
);

internal class GetQuestJournalQueryHandler(
    IQuestsDbContext context,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds
) : IQueryHandler<GetQuestJournalQuery, IReadOnlyCollection<QuestJournalEntry>>
{
    public async Task<IReadOnlyCollection<QuestJournalEntry>> Handle(
        GetQuestJournalQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var quests = await context
            .CreatureQuests.AsNoTracking()
            .Include(creatureQuest => creatureQuest.Quest)
            .Where(creatureQuest =>
                creatureQuest.CreatureId == query.PlayerId && creatureQuest.WorldId == query.WorldId
            )
            .OrderBy(creatureQuest => creatureQuest.Status)
            .ThenBy(creatureQuest => creatureQuest.Quest.Name)
            .ToArrayAsync(cancellationToken);

        var objectives = await GetObjectives(query.PlayerId, query.WorldId, cancellationToken);

        var locationIds = objectives
            .Select(objective => objective.Objective.LocationId)
            .OfType<Guid>()
            .Distinct()
            .ToArray();

        var locationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = locationIds },
            cancellationToken
        );
        var locationNamesById = locationsById.ToDictionary(kv => kv.Key, kv => kv.Value.Name);

        var objectivesByQuestId = objectives
            .GroupBy(objective => objective.Objective.QuestId)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .Select(objective =>
                            ToProgress(
                                objective,
                                objective.Objective.LocationId is { } locationId
                                    ? locationNamesById.GetValueOrDefault(locationId)
                                    : null
                            )
                        )
                        .ToArray()
            );

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

    private static QuestObjectiveProgress ToProgress(
        CreatureQuestObjective objective,
        string? locationName
    ) =>
        new(
            objective.Objective.Name,
            objective.Objective.Description,
            objective.Amount,
            objective.Objective.RequiredAmount,
            locationName
        );
}
