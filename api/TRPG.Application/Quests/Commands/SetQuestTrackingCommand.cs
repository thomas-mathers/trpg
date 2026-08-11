using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Data;

namespace TRPG.Application.Quests.Commands;

internal class SetQuestTrackingCommand
{
    public required bool IsTracked { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class SetQuestTrackingCommandHandler(TrpgDbContext context)
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
