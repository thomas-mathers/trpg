using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public enum QuestMarker
{
    Available,
    ReadyToTurnIn,
}

public class GetQuestMarkersForGiversQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> GiverIds { get; init; }
}

internal class GetQuestMarkersForGiversQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetQuestMarkersForGiversQuery, IReadOnlyDictionary<Guid, QuestMarker>>
{
    public async Task<IReadOnlyDictionary<Guid, QuestMarker>> Handle(
        GetQuestMarkersForGiversQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.GiverIds.Count == 0)
        {
            return new Dictionary<Guid, QuestMarker>();
        }

        var quests = await context
            .Quests.AsNoTracking()
            .Where(quest =>
                quest.WorldId == query.WorldId
                && query.GiverIds.AsEnumerable().Contains(quest.GiverId)
            )
            .ToArrayAsync(cancellationToken);
        var questIds = quests.Select(quest => quest.Id).ToArray();

        if (questIds.Length == 0)
        {
            return new Dictionary<Guid, QuestMarker>();
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
        var markers = new Dictionary<Guid, QuestMarker>();

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

        return markers;
    }
}
