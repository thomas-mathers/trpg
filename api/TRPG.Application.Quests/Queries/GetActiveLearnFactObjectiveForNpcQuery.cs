using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

// Lets a conversation tool find which fact an NPC is withholding without the LLM ever supplying a
// FactId itself — it only ever knows the NPC by name. GetActiveLearnFactObjectiveQuery is the
// FactId-known counterpart the resolved commands use once this has found it.
public class GetActiveLearnFactObjectiveForNpcQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid NpcId { get; init; }
}

internal class GetActiveLearnFactObjectiveForNpcQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetActiveLearnFactObjectiveForNpcQuery, LearnFactFromCreatureObjective?>
{
    public async Task<LearnFactFromCreatureObjective?> Handle(
        GetActiveLearnFactObjectiveForNpcQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .QuestObjectives.OfType<LearnFactFromCreatureObjective>()
            .AsNoTracking()
            .Where(objective =>
                objective.WorldId == query.WorldId
                && objective.CreatureId == query.NpcId
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
