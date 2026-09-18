using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Knowledge.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public enum QuestMarker
{
    Available,
    ReadyToTurnIn,
}

public record QuestMarkerEntry(Guid QuestId, string Name, QuestMarker Marker);

public record QuestMarkersResult(
    IReadOnlyDictionary<Guid, IReadOnlyCollection<QuestMarkerEntry>> EntriesByCreatureId,
    IReadOnlySet<Guid> ReadyToDeliverCreatureIds
);

public class GetQuestMarkersForCreaturesQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetQuestMarkersForCreaturesQueryHandler(
    IQueryHandler<GetKnownFactIdsQuery, IReadOnlyList<Guid>> getKnownFacts,
    IQuestsDbContext context
) : IQueryHandler<GetQuestMarkersForCreaturesQuery, QuestMarkersResult>
{
    public async Task<QuestMarkersResult> Handle(
        GetQuestMarkersForCreaturesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.CreatureIds.Count == 0)
        {
            return new QuestMarkersResult(
                new Dictionary<Guid, IReadOnlyCollection<QuestMarkerEntry>>(),
                new HashSet<Guid>()
            );
        }

        var entriesByCreatureId = new Dictionary<Guid, List<QuestMarkerEntry>>();
        var readyToDeliverCreatureIds = new HashSet<Guid>();

        await AddGiverMarkers(query, entriesByCreatureId, cancellationToken);
        await AddDeliveryMarkers(query, readyToDeliverCreatureIds, cancellationToken);

        return new QuestMarkersResult(
            entriesByCreatureId.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyCollection<QuestMarkerEntry>)pair.Value.ToArray()
            ),
            readyToDeliverCreatureIds
        );
    }

    private async Task AddGiverMarkers(
        GetQuestMarkersForCreaturesQuery query,
        Dictionary<Guid, List<QuestMarkerEntry>> entriesByCreatureId,
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

        var prerequisiteQuestIds = quests.SelectMany(quest => quest.PrerequisiteQuestIds).ToArray();
        var prerequisiteGroupIdsByQuestId = await context
            .Quests.AsNoTracking()
            .Where(quest => prerequisiteQuestIds.AsEnumerable().Contains(quest.Id))
            .Select(quest => new { quest.Id, quest.ExclusiveGroupId })
            .ToDictionaryAsync(
                quest => quest.Id,
                quest => quest.ExclusiveGroupId,
                cancellationToken
            );
        var siblingQuestIdsByGroupId = await context
            .Quests.AsNoTracking()
            .Where(quest =>
                quest.WorldId == query.WorldId
                && quest.ExclusiveGroupId != null
                && quests
                    .Select(candidate => candidate.ExclusiveGroupId)
                    .Contains(quest.ExclusiveGroupId)
            )
            .GroupBy(quest => quest.ExclusiveGroupId!.Value)
            .ToDictionaryAsync(
                group => group.Key,
                group => (IReadOnlyCollection<Guid>)group.Select(quest => quest.Id).ToArray(),
                cancellationToken
            );

        var knownFacts = await getKnownFacts.Handle(
            new GetKnownFactIdsQuery(query.WorldId, query.PlayerId),
            cancellationToken
        );

        foreach (var quest in quests)
        {
            if (
                !playerQuestByQuestId.ContainsKey(quest.Id)
                && (quest.RequiredFactId == null || knownFacts.Contains(quest.RequiredFactId.Value))
                && QuestExclusiveGroupEvaluator.ArePrerequisitesSatisfied(
                    quest.PrerequisiteQuestIds,
                    prerequisiteGroupIdsByQuestId,
                    completedQuestIdSet
                )
                && !QuestExclusiveGroupEvaluator.IsClosedBySiblingCompletion(
                    quest.Id,
                    quest.ExclusiveGroupId,
                    siblingQuestIdsByGroupId,
                    completedQuestIdSet
                )
            )
            {
                AddEntry(
                    entriesByCreatureId,
                    quest.GiverId,
                    new QuestMarkerEntry(quest.Id, quest.Name, QuestMarker.Available)
                );
            }
        }

        var questsById = quests.ToDictionary(quest => quest.Id);
        foreach (
            var playerQuest in playerQuests.Where(quest =>
                quest.Status == QuestStatus.ReadyToComplete
            )
        )
        {
            var quest = questsById[playerQuest.QuestId];
            AddEntry(
                entriesByCreatureId,
                quest.GiverId,
                new QuestMarkerEntry(quest.Id, quest.Name, QuestMarker.ReadyToTurnIn)
            );
        }
    }

    private static void AddEntry(
        Dictionary<Guid, List<QuestMarkerEntry>> entriesByCreatureId,
        Guid creatureId,
        QuestMarkerEntry entry
    )
    {
        if (!entriesByCreatureId.TryGetValue(creatureId, out var entries))
        {
            entries = [];
            entriesByCreatureId[creatureId] = entries;
        }

        entries.Add(entry);
    }

    private async Task AddDeliveryMarkers(
        GetQuestMarkersForCreaturesQuery query,
        HashSet<Guid> readyToDeliverCreatureIds,
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
            readyToDeliverCreatureIds.Add(recipientId);
        }
    }
}
