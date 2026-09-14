using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public record DeliverableItemResult(Guid QuestId, string QuestName, Guid ItemId);

public class GetDeliverableItemForRecipientQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid RecipientId { get; init; }
}

internal class GetDeliverableItemForRecipientQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetDeliverableItemForRecipientQuery, DeliverableItemResult?>
{
    public async Task<DeliverableItemResult?> Handle(
        GetDeliverableItemForRecipientQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var acceptedQuestIds = await context
            .CreatureQuests.AsNoTracking()
            .Where(creatureQuest =>
                creatureQuest.CreatureId == query.PlayerId
                && creatureQuest.WorldId == query.WorldId
                && creatureQuest.Status == QuestStatus.Accepted
            )
            .Select(creatureQuest => creatureQuest.QuestId)
            .ToArrayAsync(cancellationToken);

        if (acceptedQuestIds.Length == 0)
        {
            return null;
        }

        var pending = await context
            .QuestObjectives.AsNoTracking()
            .OfType<DeliverItemObjective>()
            .Where(objective =>
                acceptedQuestIds.AsEnumerable().Contains(objective.QuestId)
                && objective.RecipientId == query.RecipientId
            )
            .Join(
                context.CreatureQuestObjectives.Where(progress =>
                    progress.CreatureId == query.PlayerId
                ),
                objective => objective.Id,
                progress => progress.ObjectiveId,
                (objective, progress) =>
                    new
                    {
                        objective.QuestId,
                        objective.ItemId,
                        progress.Amount,
                        objective.RequiredAmount,
                    }
            )
            .Where(candidate => candidate.Amount < candidate.RequiredAmount)
            .FirstOrDefaultAsync(cancellationToken);

        if (pending == null)
        {
            return null;
        }

        var quest = await context
            .Quests.AsNoTracking()
            .FirstAsync(q => q.Id == pending.QuestId, cancellationToken);

        return new DeliverableItemResult(quest.Id, quest.Name, pending.ItemId);
    }
}
