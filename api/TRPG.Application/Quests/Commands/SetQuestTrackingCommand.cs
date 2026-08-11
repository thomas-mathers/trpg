using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Commands;

internal class SetQuestTrackingCommand
{
    public required bool IsTracked { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
}

internal class SetQuestTrackingCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        SetQuestTrackingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var playerWorldId = await context
            .Creatures.Where(creature => creature.Id == command.PlayerId)
            .Select(creature => (Guid?)creature.WorldId)
            .FirstOrDefaultAsync(cancellationToken);
        if (playerWorldId is null)
        {
            throw new EntityNotFoundException("Player", command.PlayerId);
        }

        var updated = await context
            .CreatureQuests.Where(quest =>
                quest.CreatureId == command.PlayerId
                && quest.QuestId == command.QuestId
                && quest.WorldId == playerWorldId.Value
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
            throw new EntityNotFoundException("Active quest", command.QuestId);
        }
    }
}
