using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public record GetReportFactIdsForQuestQuery(Guid QuestId);

internal sealed class GetReportFactIdsForQuestQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetReportFactIdsForQuestQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetReportFactIdsForQuestQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .QuestObjectives.AsNoTracking()
            .OfType<ReportFactToCreatureObjective>()
            .Where(objective => objective.QuestId == query.QuestId)
            .Select(objective => objective.FactId)
            .ToArrayAsync(cancellationToken);
}
