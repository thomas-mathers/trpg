using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Inventory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Quests.Commands;

internal class CompleteQuestCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class CompleteQuestCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        CompleteQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creatureQuest = await context
            .CreatureQuests.Include(quest => quest.Quest)
            .FirstOrDefaultAsync(
                quest =>
                    quest.CreatureId == command.PlayerId
                    && quest.QuestId == command.QuestId
                    && quest.WorldId == command.WorldId,
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

        var gold = await context
            .Items.OfType<Gold>()
            .FirstOrDefaultAsync(
                item =>
                    item.Ownership.OwnerId == command.PlayerId
                    && item.Ownership.OwnerType == OwnerType.Creature,
                cancellationToken
            );
        if (gold is null)
        {
            gold = new Gold
            {
                WorldId = command.WorldId,
                Name = "Gold",
                Ownership = new ItemOwnership
                {
                    OwnerId = command.PlayerId,
                    OwnerType = OwnerType.Creature,
                },
            };
            context.Items.Add(gold);
        }

        gold.Quantity += creatureQuest.Quest.GoldReward;
        creatureQuest.Status = QuestStatus.Completed;

        await context.SaveChangesAsync(cancellationToken);
    }
}
