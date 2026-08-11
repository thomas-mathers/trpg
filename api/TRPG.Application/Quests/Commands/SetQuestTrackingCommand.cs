using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Commands;

internal class SetQuestTrackingCommand
{
    public required bool IsTracked { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
}

internal class SetQuestTrackingCommandHandler(
    TrpgDbContext context,
    GetCreatureWorldIdQueryHandler getCreatureWorldId
)
{
    public async Task Handle(
        SetQuestTrackingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var playerWorldId = await getCreatureWorldId.Handle(
            new GetCreatureWorldIdQuery { CreatureId = command.PlayerId },
            cancellationToken
        );

        var updated = await context
            .CreatureQuests.Where(quest =>
                quest.CreatureId == command.PlayerId
                && quest.QuestId == command.QuestId
                && quest.WorldId == playerWorldId
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
