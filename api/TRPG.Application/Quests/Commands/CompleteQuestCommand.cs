using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Commands;

internal class CompleteQuestCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
}

internal class CompleteQuestCommandHandler(
    TrpgDbContext context,
    AddGoldCommandHandler addGold,
    GetCreatureWorldIdQueryHandler getCreatureWorldId
)
{
    public async Task Handle(
        CompleteQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var playerWorldId = await getCreatureWorldId.Handle(
            new GetCreatureWorldIdQuery { CreatureId = command.PlayerId },
            cancellationToken
        );

        var creatureQuest = await context
            .CreatureQuests.Include(quest => quest.Quest)
            .FirstOrDefaultAsync(
                quest =>
                    quest.CreatureId == command.PlayerId
                    && quest.QuestId == command.QuestId
                    && quest.WorldId == playerWorldId,
                cancellationToken
            );
        if (creatureQuest is null)
        {
            throw new EntityNotFoundException("Accepted quest", command.QuestId);
        }

        if (creatureQuest.Status != QuestStatus.ReadyToComplete)
        {
            throw new InvalidOperationException("Quest objectives have not all been completed.");
        }

        await addGold.Handle(
            new AddGoldCommand
            {
                Amount = creatureQuest.Quest.GoldReward,
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                WorldId = playerWorldId,
            },
            cancellationToken
        );
        creatureQuest.Status = QuestStatus.Completed;

        await context.SaveChangesAsync(cancellationToken);
    }
}
