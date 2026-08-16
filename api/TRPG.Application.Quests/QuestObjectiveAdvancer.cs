using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Events;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests;

internal sealed class QuestObjectiveAdvancer(
    ICommandHandler<MarkQuestsReadyToCompleteCommand> markQuestsReadyToComplete,
    IGameClientEventSink gameEvents,
    TrpgDbContext context
)
{
    public async Task Advance(
        Guid playerId,
        Guid worldId,
        Func<QuestObjective, bool> matches,
        CancellationToken cancellationToken
    )
    {
        var objectives = await context
            .CreatureQuestObjectives.Include(progress => progress.Objective)
            .Where(progress =>
                progress.CreatureId == playerId
                && progress.WorldId == worldId
                && context.CreatureQuests.Any(quest =>
                    quest.CreatureId == playerId
                    && quest.QuestId == progress.Objective.QuestId
                    && quest.Status == QuestStatus.Accepted
                )
            )
            .ToListAsync(cancellationToken);

        var progressedObjectives = objectives
            .Where(objective => matches(objective.Objective))
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
                PlayerId = playerId,
                QuestIds = progressedObjectives
                    .Select(objective => objective.Objective.QuestId)
                    .ToArray(),
                WorldId = worldId,
            },
            cancellationToken
        );

        await context.SaveChangesAsync(cancellationToken);

        foreach (
            var objective in progressedObjectives.Where(objective =>
                objective.Amount == objective.Objective.RequiredAmount
            )
        )
        {
            gameEvents.Enqueue(new QuestObjectiveCompletedEvent(objective.Objective.Name));
        }

        gameEvents.Enqueue(new QuestJournalUpdatedEvent());
    }

    private static bool CanAdvance(CreatureQuestObjective objective) =>
        objective.Amount < objective.Objective.RequiredAmount;
}
