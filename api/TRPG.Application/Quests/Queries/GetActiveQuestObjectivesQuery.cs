using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Queries;

internal class GetActiveQuestObjectivesQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GetActiveQuestObjectivesQueryHandler(TrpgDbContext context)
{
    public Task<List<CreatureQuestObjective>> Handle(
        GetActiveQuestObjectivesQuery query,
        CancellationToken cancellationToken = default
    ) =>
        context
            .CreatureQuestObjectives.Include(progress => progress.Objective)
            .Where(progress =>
                progress.CreatureId == query.PlayerId
                && progress.WorldId == query.WorldId
                && context.CreatureQuests.Any(quest =>
                    quest.CreatureId == query.PlayerId
                    && quest.QuestId == progress.Objective.QuestId
                    && quest.Status == QuestStatus.Accepted
                )
            )
            .ToListAsync(cancellationToken);
}
