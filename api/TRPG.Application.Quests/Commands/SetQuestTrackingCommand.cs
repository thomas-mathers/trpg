using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Validation;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Commands;

public class SetQuestTrackingCommand
{
    public required bool IsTracked { get; init; }

    [NotEmptyGuid]
    public required Guid PlayerId { get; init; }

    [NotEmptyGuid]
    public required Guid QuestId { get; init; }

    [NotEmptyGuid]
    public required Guid WorldId { get; init; }
}

internal class SetQuestTrackingCommandHandler(IQuestsDbContext context)
    : ICommandHandler<SetQuestTrackingCommand>
{
    public async Task Handle(
        SetQuestTrackingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var updated = await context
            .CreatureQuests.Where(quest =>
                quest.CreatureId == command.PlayerId
                && quest.QuestId == command.QuestId
                && quest.WorldId == command.WorldId
                && (
                    quest.Status == QuestStatus.Accepted
                    || quest.Status == QuestStatus.ReadyToComplete
                )
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(quest => quest.IsTracked, command.IsTracked),
                cancellationToken
            );

        if (updated == 0)
        {
            throw new EntityNotFoundException("Quest", command.QuestId);
        }
    }
}
