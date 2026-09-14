using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

// Per-player and only while the quest is still open, unlike GetRescueQuestParticipantIdsQuery's
// world-wide-forever exclusion — a dungeon that's already been cleared and turned in must become
// offerable again, that's the point of a repeatable quest.
public class GetActiveClearLocationObjectiveBuildingIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class GetActiveClearLocationObjectiveBuildingIdsQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetActiveClearLocationObjectiveBuildingIdsQuery, IReadOnlySet<Guid>>
{
    private static readonly QuestStatus[] ActiveStatuses =
    [
        QuestStatus.Accepted,
        QuestStatus.ReadyToComplete,
    ];

    public async Task<IReadOnlySet<Guid>> Handle(
        GetActiveClearLocationObjectiveBuildingIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var activeQuestIds = await context
            .CreatureQuests.AsNoTracking()
            .Where(quest =>
                quest.CreatureId == query.PlayerId
                && quest.WorldId == query.WorldId
                && ActiveStatuses.AsEnumerable().Contains(quest.Status)
            )
            .Select(quest => quest.QuestId)
            .ToArrayAsync(cancellationToken);

        if (activeQuestIds.Length == 0)
        {
            return new HashSet<Guid>();
        }

        var buildingIds = await context
            .QuestObjectives.AsNoTracking()
            .OfType<ClearLocationObjective>()
            .Where(objective =>
                objective.WorldId == query.WorldId
                && activeQuestIds.AsEnumerable().Contains(objective.QuestId)
            )
            .Select(objective => objective.BuildingId)
            .ToArrayAsync(cancellationToken);

        return buildingIds.ToHashSet();
    }
}
