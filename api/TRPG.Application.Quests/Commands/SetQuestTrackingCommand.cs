using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Handling;
using TRPG.Data;

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

internal class SetQuestTrackingCommandHandler(TrpgDbContext context)
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
