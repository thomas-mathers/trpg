using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
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
    FindOrCreateGoldItemCommandHandler findOrCreateGoldItem
)
{
    public async Task Handle(
        CompleteQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await context.Creatures.FindAsync([command.PlayerId], cancellationToken);
        if (player is null)
        {
            throw new EntityNotFoundException("Player", command.PlayerId);
        }

        var creatureQuest = await context
            .CreatureQuests.Include(quest => quest.Quest)
            .FirstOrDefaultAsync(
                quest =>
                    quest.CreatureId == player.Id
                    && quest.QuestId == command.QuestId
                    && quest.WorldId == player.WorldId,
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

        var gold = await findOrCreateGoldItem.Handle(
            new FindOrCreateGoldItemCommand
            {
                Owner = new ItemOwnerReference(player.Id, OwnerType.Creature),
                WorldId = player.WorldId,
            },
            cancellationToken
        );
        gold.Quantity += creatureQuest.Quest.GoldReward;
        creatureQuest.Status = QuestStatus.Completed;

        await context.SaveChangesAsync(cancellationToken);
    }
}
