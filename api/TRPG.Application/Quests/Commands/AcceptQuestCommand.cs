using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Commands;

internal class AcceptQuestCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
}

internal class AcceptQuestCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        AcceptQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await context.Creatures.FindAsync([command.PlayerId], cancellationToken);
        if (player is null)
        {
            throw new EntityNotFoundException("Player", command.PlayerId);
        }

        var quest = await context.Quests.FirstOrDefaultAsync(
            quest => quest.Id == command.QuestId && quest.WorldId == player.WorldId,
            cancellationToken
        );
        if (quest is null)
        {
            throw new EntityNotFoundException("Quest", command.QuestId);
        }

        var completedQuestIds = await context
            .CreatureQuests.Where(creatureQuest =>
                creatureQuest.CreatureId == player.Id
                && creatureQuest.Status == QuestStatus.Completed
                && quest.PrerequisiteQuestIds.Contains(creatureQuest.QuestId)
            )
            .Select(creatureQuest => creatureQuest.QuestId)
            .ToArrayAsync(cancellationToken);
        if (completedQuestIds.Length != quest.PrerequisiteQuestIds.Count)
        {
            throw new InvalidOperationException("Quest prerequisites have not been completed.");
        }

        var objectiveIds = await context
            .QuestObjectives.AsNoTracking()
            .Where(objective => objective.QuestId == quest.Id)
            .Select(objective => objective.Id)
            .ToArrayAsync(cancellationToken);

        context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                IsTracked = true,
                WorldId = player.WorldId,
            }
        );
        context.CreatureQuestObjectives.AddRange(
            objectiveIds.Select(objectiveId => new CreatureQuestObjective
            {
                CreatureId = player.Id,
                ObjectiveId = objectiveId,
                Amount = 0,
                WorldId = player.WorldId,
            })
        );

        await context.SaveChangesAsync(cancellationToken);
    }
}
