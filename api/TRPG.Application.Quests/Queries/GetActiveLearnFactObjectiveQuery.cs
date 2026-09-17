using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public class GetActiveLearnFactObjectiveQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid NpcId { get; init; }
    public required Guid FactId { get; init; }
}

internal class GetActiveLearnFactObjectiveQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetActiveLearnFactObjectiveQuery, LearnFactFromCreatureObjective?>
{
    public async Task<LearnFactFromCreatureObjective?> Handle(
        GetActiveLearnFactObjectiveQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .QuestObjectives.OfType<LearnFactFromCreatureObjective>()
            .AsNoTracking()
            .Where(objective =>
                objective.WorldId == query.WorldId
                && objective.CreatureId == query.NpcId
                && objective.FactId == query.FactId
                && context.CreatureQuestObjectives.Any(progress =>
                    progress.CreatureId == query.PlayerId
                    && progress.ObjectiveId == objective.Id
                    && progress.Amount < objective.RequiredAmount
                )
                && context.CreatureQuests.Any(creatureQuest =>
                    creatureQuest.CreatureId == query.PlayerId
                    && creatureQuest.QuestId == objective.QuestId
                    && creatureQuest.Status == QuestStatus.Accepted
                )
            )
            .FirstOrDefaultAsync(cancellationToken);
}
