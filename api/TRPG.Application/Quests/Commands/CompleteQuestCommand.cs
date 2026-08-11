using System.Transactions;
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
    public required Guid WorldId { get; init; }
}

internal class CompleteQuestCommandHandler(TrpgDbContext context, AddGoldCommandHandler addGold)
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

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await addGold.Handle(
            new AddGoldCommand
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                WorldId = command.WorldId,
                Amount = creatureQuest.Quest.GoldReward,
            },
            cancellationToken
        );

        creatureQuest.Status = QuestStatus.Completed;

        await context.SaveChangesAsync(cancellationToken);
        transaction.Complete();
    }
}
