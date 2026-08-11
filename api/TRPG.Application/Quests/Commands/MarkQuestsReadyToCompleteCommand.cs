using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Commands;

internal class MarkQuestsReadyToCompleteCommand
{
    public required Guid PlayerId { get; init; }
    public required IReadOnlyCollection<Guid> QuestIds { get; init; }
    public required Guid WorldId { get; init; }
}

internal class MarkQuestsReadyToCompleteCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        MarkQuestsReadyToCompleteCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.QuestIds.Count == 0)
        {
            return;
        }

        var objectives = await context
            .CreatureQuestObjectives.Include(progress => progress.Objective)
            .Where(progress =>
                progress.CreatureId == command.PlayerId
                && progress.WorldId == command.WorldId
                && command.QuestIds.Contains(progress.Objective.QuestId)
            )
            .ToArrayAsync(cancellationToken);
        var readyQuestIds = objectives
            .GroupBy(progress => progress.Objective.QuestId)
            .Where(group => group.All(IsComplete))
            .Select(group => group.Key)
            .ToArray();
        if (readyQuestIds.Length == 0)
        {
            return;
        }

        var quests = await context
            .CreatureQuests.Where(quest =>
                quest.CreatureId == command.PlayerId
                && quest.WorldId == command.WorldId
                && quest.Status == QuestStatus.Accepted
                && readyQuestIds.Contains(quest.QuestId)
            )
            .ToListAsync(cancellationToken);
        foreach (var quest in quests)
        {
            quest.Status = QuestStatus.ReadyToComplete;
        }
    }

    private static bool IsComplete(CreatureQuestObjective objective) =>
        objective.Amount
        >= (
            objective.Objective switch
            {
                KillCreatureTypeObjective kill => kill.RequiredAmount,
                CollectItemObjective collect => collect.RequiredAmount,
                _ => 1,
            }
        );
}
