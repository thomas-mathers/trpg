using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Quests.Queries;

public class GetInProgressLocationObjectivesQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

public record InProgressLocationObjective(Guid QuestId, string ObjectiveName, Guid LocationId);

internal class GetInProgressLocationObjectivesQueryHandler(TrpgDbContext context)
    : IQueryHandler<
        GetInProgressLocationObjectivesQuery,
        IReadOnlyCollection<InProgressLocationObjective>
    >
{
    public async Task<IReadOnlyCollection<InProgressLocationObjective>> Handle(
        GetInProgressLocationObjectivesQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .CreatureQuestObjectives.AsNoTracking()
            .Include(objective => objective.Objective)
            .Where(objective =>
                objective.CreatureId == query.PlayerId && objective.WorldId == query.WorldId
            )
            .Where(objective => objective.Amount < objective.Objective.RequiredAmount)
            .Where(objective => objective.Objective.LocationId != null)
            .Select(objective => new InProgressLocationObjective(
                objective.Objective.QuestId,
                objective.Objective.Name,
                objective.Objective.LocationId!.Value
            ))
            .ToArrayAsync(cancellationToken);
}
