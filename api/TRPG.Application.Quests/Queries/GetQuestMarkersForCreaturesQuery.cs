using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public enum QuestMarker
{
    Available,
    ReadyToTurnIn,
    ReadyToDeliver,
}

public class GetQuestMarkersForCreaturesQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetQuestMarkersForCreaturesQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetQuestMarkersForCreaturesQuery, IReadOnlyDictionary<Guid, QuestMarker>>
{
    public async Task<IReadOnlyDictionary<Guid, QuestMarker>> Handle(
        GetQuestMarkersForCreaturesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.CreatureIds.Count == 0)
        {
            return new Dictionary<Guid, QuestMarker>();
        }

        var markers = new Dictionary<Guid, QuestMarker>();

        await AddGiverMarkers(query, markers, cancellationToken);
        await AddDeliveryMarkers(query, markers, cancellationToken);

        return markers;
    }

    private async Task AddGiverMarkers(
        GetQuestMarkersForCreaturesQuery query,
        Dictionary<Guid, QuestMarker> markers,
        CancellationToken cancellationToken
    )
    {
        var quests = await context
            .Quests.AsNoTracking()
            .Where(quest =>
                quest.WorldId == query.WorldId
                && query.CreatureIds.AsEnumerable().Contains(quest.GiverId)
            )
            .ToArrayAsync(cancellationToken);
        var questIds = quests.Select(quest => quest.Id).ToArray();

        if (questIds.Length == 0)
        {
            return;
        }

        var playerQuests = await context
            .CreatureQuests.AsNoTracking()
            .Where(creatureQuest =>
                creatureQuest.CreatureId == query.PlayerId
                && creatureQuest.WorldId == query.WorldId
                && questIds.AsEnumerable().Contains(creatureQuest.QuestId)
            )
            .ToArrayAsync(cancellationToken);
        var completedQuestIds = await context
            .CreatureQuests.AsNoTracking()
            .Where(creatureQuest =>
                creatureQuest.CreatureId == query.PlayerId
                && creatureQuest.WorldId == query.WorldId
                && creatureQuest.Status == QuestStatus.Completed
            )
            .Select(creatureQuest => creatureQuest.QuestId)
            .ToArrayAsync(cancellationToken);

        var completedQuestIdSet = completedQuestIds.ToHashSet();
        var playerQuestByQuestId = playerQuests.ToDictionary(quest => quest.QuestId);

        foreach (var quest in quests)
        {
            if (
                !playerQuestByQuestId.ContainsKey(quest.Id)
                && quest.PrerequisiteQuestIds.All(completedQuestIdSet.Contains)
            )
            {
                markers.TryAdd(quest.GiverId, QuestMarker.Available);
            }
        }

        var questsById = quests.ToDictionary(quest => quest.Id);
        foreach (
            var playerQuest in playerQuests.Where(quest =>
                quest.Status == QuestStatus.ReadyToComplete
            )
        )
        {
            markers[questsById[playerQuest.QuestId].GiverId] = QuestMarker.ReadyToTurnIn;
        }
    }

    private async Task AddDeliveryMarkers(
        GetQuestMarkersForCreaturesQuery query,
        Dictionary<Guid, QuestMarker> markers,
        CancellationToken cancellationToken
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
            return;
        }

        var pendingRecipientIds = await context
            .QuestObjectives.AsNoTracking()
            .OfType<DeliverItemObjective>()
            .Where(objective =>
                acceptedQuestIds.AsEnumerable().Contains(objective.QuestId)
                && query.CreatureIds.AsEnumerable().Contains(objective.RecipientId)
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
                        objective.RecipientId,
                        progress.Amount,
                        objective.RequiredAmount,
                    }
            )
            .Where(pending => pending.Amount < pending.RequiredAmount)
            .Select(pending => pending.RecipientId)
            .ToArrayAsync(cancellationToken);

        foreach (var recipientId in pendingRecipientIds)
        {
            markers[recipientId] = QuestMarker.ReadyToDeliver;
        }
    }
}
