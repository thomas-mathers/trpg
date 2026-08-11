using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Application.GameSessions;
using TRPG.Application.Quests.Commands;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests;

internal sealed class QuestEventHandler(
    MarkQuestsReadyToCompleteCommandHandler markQuestsReadyToComplete,
    IGameClientEventPublisher gameEvents,
    TrpgDbContext context
)
{
    public async Task Handle(GameEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var objectives = await context
            .CreatureQuestObjectives.Include(progress => progress.Objective)
            .Where(progress =>
                progress.CreatureId == gameEvent.PlayerId
                && progress.WorldId == gameEvent.WorldId
                && context.CreatureQuests.Any(quest =>
                    quest.CreatureId == gameEvent.PlayerId
                    && quest.QuestId == progress.Objective.QuestId
                    && quest.Status == QuestStatus.Accepted
                )
            )
            .ToListAsync(cancellationToken);
        var progressedObjectives = objectives
            .Where(objective => Matches(objective.Objective, gameEvent))
            .Where(CanAdvance)
            .ToArray();

        foreach (var objective in progressedObjectives)
        {
            objective.Amount++;
        }

        if (progressedObjectives.Length == 0)
        {
            return;
        }

        await markQuestsReadyToComplete.Handle(
            new MarkQuestsReadyToCompleteCommand
            {
                PlayerId = gameEvent.PlayerId,
                QuestIds = progressedObjectives
                    .Select(objective => objective.Objective.QuestId)
                    .ToArray(),
                WorldId = gameEvent.WorldId,
            },
            cancellationToken
        );
        await context.SaveChangesAsync(cancellationToken);
        gameEvents.Publish(new QuestJournalUpdatedEvent());
    }

    private static bool CanAdvance(CreatureQuestObjective objective) =>
        objective.Amount < objective.Objective.RequiredAmount;

    private static bool Matches(QuestObjective objective, GameEvent gameEvent) =>
        (objective, gameEvent) switch
        {
            (KillCreatureObjective kill, CreatureKilledEvent killed) => kill.CreatureId
                == killed.CreatureId,
            (KillCreatureTypeObjective kill, CreatureKilledEvent killed) => kill.CreatureType
                == killed.CreatureType,
            (ExploreBuildingObjective explore, PlayerMovedEvent entered) => explore.LocationId
                == entered.LocationId,
            (ExploreCityObjective explore, PlayerMovedEvent entered) => explore.LocationId
                == entered.LocationId,
            (SpeakToCreatureObjective speak, ConversationStartedEvent conversation) =>
                speak.CreatureId == conversation.CreatureId,
            (CollectItemObjective collect, ItemAcquiredEvent acquired) => collect.ItemId
                == acquired.ItemId,
            _ => false,
        };
}
