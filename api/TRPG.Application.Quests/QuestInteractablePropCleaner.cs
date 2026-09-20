using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Props.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests;

internal sealed class QuestInteractablePropCleaner(
    IQuestsDbContext context,
    ICommandHandler<DeleteTriggersCommand> deleteTriggers
)
{
    public async Task CleanUp(Guid questId, CancellationToken cancellationToken)
    {
        var triggerIds = await context
            .QuestObjectives.OfType<InteractWithPropObjective>()
            .Where(objective => objective.QuestId == questId)
            .Select(objective => objective.TriggerId)
            .ToArrayAsync(cancellationToken);

        if (triggerIds.Length == 0)
        {
            return;
        }

        await deleteTriggers.Handle(
            new DeleteTriggersCommand { TriggerIds = triggerIds },
            cancellationToken
        );
    }
}
