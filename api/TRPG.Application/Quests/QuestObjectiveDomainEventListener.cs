using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Application.GameSessions;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests;

internal sealed class QuestObjectiveDomainEventListener(TrpgDbContext context)
    : GameDomainEventListener
{
    public override async Task<IReadOnlyCollection<GameTurnEvent>> Handle(
        GameDomainEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var objectives = await GetActiveObjectives(domainEvent, cancellationToken);
        var progressedObjectives = objectives
            .Where(objective => Matches(objective.Objective, domainEvent))
            .Where(CanAdvance)
            .ToArray();

        foreach (var objective in progressedObjectives)
        {
            objective.Amount++;
        }

        await MarkReadyToComplete(objectives, domainEvent.PlayerId, cancellationToken);

        return [];
    }

    private Task<List<CreatureQuestObjective>> GetActiveObjectives(
        GameDomainEvent domainEvent,
        CancellationToken cancellationToken
    ) =>
        context
            .CreatureQuestObjectives.Include(progress => progress.Objective)
            .Where(progress =>
                progress.CreatureId == domainEvent.PlayerId
                && progress.WorldId == domainEvent.WorldId
                && context.CreatureQuests.Any(quest =>
                    quest.CreatureId == domainEvent.PlayerId
                    && quest.QuestId == progress.Objective.QuestId
                    && quest.Status == QuestStatus.Accepted
                )
            )
            .ToListAsync(cancellationToken);

    private async Task MarkReadyToComplete(
        IReadOnlyCollection<CreatureQuestObjective> objectives,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var objectiveGroups = objectives.GroupBy(objective => objective.Objective.QuestId);
        var readyQuestIds = objectiveGroups
            .Where(group => group.All(IsComplete))
            .Select(group => group.Key)
            .ToArray();
        if (readyQuestIds.Length == 0)
        {
            return;
        }

        var quests = await context
            .CreatureQuests.Where(quest =>
                quest.CreatureId == playerId
                && quest.Status == QuestStatus.Accepted
                && readyQuestIds.Contains(quest.QuestId)
            )
            .ToListAsync(cancellationToken);

        foreach (var quest in quests)
        {
            quest.Status = QuestStatus.ReadyToComplete;
        }
    }

    private static bool CanAdvance(CreatureQuestObjective objective) =>
        objective.Amount < RequiredAmount(objective.Objective);

    private static bool IsComplete(CreatureQuestObjective objective) =>
        objective.Amount >= RequiredAmount(objective.Objective);

    private static int RequiredAmount(QuestObjective objective) =>
        objective switch
        {
            KillCreatureTypeObjective killCreatureType => killCreatureType.RequiredAmount,
            CollectItemObjective collectItem => collectItem.RequiredAmount,
            _ => 1,
        };

    private static bool Matches(QuestObjective objective, GameDomainEvent domainEvent) =>
        (objective, domainEvent) switch
        {
            (KillCreatureObjective kill, CreatureKilledDomainEvent killed) => kill.CreatureId
                == killed.CreatureId,
            (KillCreatureTypeObjective kill, CreatureKilledDomainEvent killed) => kill.CreatureType
                == killed.CreatureType,
            (ExploreBuildingObjective explore, PlayerEnteredLocationDomainEvent entered) =>
                explore.LocationId == entered.LocationId,
            (ExploreCityObjective explore, PlayerEnteredLocationDomainEvent entered) =>
                explore.LocationId == entered.LocationId,
            (SpeakToCreatureObjective speak, ConversationStartedDomainEvent conversation) =>
                speak.CreatureId == conversation.CreatureId,
            (CollectItemObjective collect, ItemAcquiredDomainEvent acquired) => collect.ItemId
                == acquired.ItemId,
            _ => false,
        };
}
