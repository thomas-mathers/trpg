using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Commands;

public class FailExclusiveGroupSiblingsCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid CompletedQuestId { get; init; }
}

internal class FailExclusiveGroupSiblingsCommandHandler(IQuestsDbContext context)
    : ICommandHandler<FailExclusiveGroupSiblingsCommand>
{
    public async Task Handle(
        FailExclusiveGroupSiblingsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var exclusiveGroupId = await context
            .Quests.AsNoTracking()
            .Where(quest =>
                quest.Id == command.CompletedQuestId && quest.WorldId == command.WorldId
            )
            .Select(quest => quest.ExclusiveGroupId)
            .SingleOrDefaultAsync(cancellationToken);
        if (exclusiveGroupId == null)
        {
            return;
        }

        var siblingQuestIds = await context
            .Quests.AsNoTracking()
            .Where(quest =>
                quest.WorldId == command.WorldId
                && quest.ExclusiveGroupId == exclusiveGroupId
                && quest.Id != command.CompletedQuestId
            )
            .Select(quest => quest.Id)
            .ToArrayAsync(cancellationToken);
        if (siblingQuestIds.Length == 0)
        {
            return;
        }

        var siblingPlayerQuests = await context
            .CreatureQuests.Where(creatureQuest =>
                creatureQuest.WorldId == command.WorldId
                && creatureQuest.CreatureId == command.PlayerId
                && siblingQuestIds.AsEnumerable().Contains(creatureQuest.QuestId)
                && (
                    creatureQuest.Status == QuestStatus.Accepted
                    || creatureQuest.Status == QuestStatus.ReadyToComplete
                )
            )
            .ToArrayAsync(cancellationToken);

        foreach (var siblingPlayerQuest in siblingPlayerQuests)
        {
            siblingPlayerQuest.Status = QuestStatus.Failed;
            siblingPlayerQuest.IsTracked = false;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
