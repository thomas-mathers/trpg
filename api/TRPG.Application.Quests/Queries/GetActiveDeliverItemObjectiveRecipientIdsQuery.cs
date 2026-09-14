using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

// Per-player and only while the quest is still open, same repeatability shape as
// GetActiveClearLocationObjectiveBuildingIdsQuery — a recipient who already received their
// delivery becomes eligible for another one.
public class GetActiveDeliverItemObjectiveRecipientIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class GetActiveDeliverItemObjectiveRecipientIdsQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetActiveDeliverItemObjectiveRecipientIdsQuery, IReadOnlySet<Guid>>
{
    private static readonly QuestStatus[] ActiveStatuses =
    [
        QuestStatus.Accepted,
        QuestStatus.ReadyToComplete,
    ];

    public async Task<IReadOnlySet<Guid>> Handle(
        GetActiveDeliverItemObjectiveRecipientIdsQuery query,
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

        var recipientIds = await context
            .QuestObjectives.AsNoTracking()
            .OfType<DeliverItemObjective>()
            .Where(objective =>
                objective.WorldId == query.WorldId
                && activeQuestIds.AsEnumerable().Contains(objective.QuestId)
            )
            .Select(objective => objective.RecipientId)
            .ToArrayAsync(cancellationToken);

        return recipientIds.ToHashSet();
    }
}
