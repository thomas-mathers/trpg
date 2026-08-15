using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Commands;

public class AcceptQuestCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class AcceptQuestCommandHandler(TrpgDbContext context)
    : ICommandHandler<AcceptQuestCommand>
{
    public async Task Handle(
        AcceptQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var quest = await context.Quests.FirstOrDefaultAsync(
            quest => quest.Id == command.QuestId && quest.WorldId == command.WorldId,
            cancellationToken
        );
        if (quest is null)
        {
            throw new EntityNotFoundException("Quest", command.QuestId);
        }

        var completedQuestIds = await context
            .CreatureQuests.Where(creatureQuest =>
                creatureQuest.CreatureId == command.PlayerId
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
                CreatureId = command.PlayerId,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                IsTracked = true,
                WorldId = command.WorldId,
            }
        );
        context.CreatureQuestObjectives.AddRange(
            objectiveIds.Select(objectiveId => new CreatureQuestObjective
            {
                CreatureId = command.PlayerId,
                ObjectiveId = objectiveId,
                Amount = 0,
                WorldId = command.WorldId,
            })
        );

        await context.SaveChangesAsync(cancellationToken);
    }
}
