using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public class GetActiveQuestItemIdsQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetActiveQuestItemIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetActiveQuestItemIdsQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetActiveQuestItemIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .CreatureQuestObjectives.AsNoTracking()
            .Where(progress =>
                progress.CreatureId == query.PlayerId
                && context.CreatureQuests.Any(quest =>
                    quest.CreatureId == query.PlayerId
                    && quest.QuestId == progress.Objective.QuestId
                    && (
                        quest.Status == QuestStatus.Accepted
                        || quest.Status == QuestStatus.ReadyToComplete
                    )
                )
            )
            .Select(progress => progress.Objective)
            .OfType<CollectItemObjective>()
            .Select(objective => objective.ItemId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
}
