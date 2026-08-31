using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Application.Quests.Events;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class CreatureKilledQuestGiverEventHandler(
    TrpgDbContext context,
    IGameClientEventSink gameEvents
) : IDomainEventConsumer<CreatureKilledEvent>
{
    public async Task Handle(
        CreatureKilledEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var quests = await context
            .CreatureQuests.Include(creatureQuest => creatureQuest.Quest)
            .Where(creatureQuest =>
                creatureQuest.CreatureId == domainEvent.PlayerId
                && creatureQuest.WorldId == domainEvent.WorldId
                && creatureQuest.Quest.GiverId == domainEvent.CreatureId
                && (
                    creatureQuest.Status == QuestStatus.Accepted
                    || creatureQuest.Status == QuestStatus.ReadyToComplete
                )
            )
            .ToArrayAsync(cancellationToken);

        if (quests.Length == 0)
        {
            return;
        }

        foreach (var quest in quests)
        {
            quest.Status = QuestStatus.Failed;
            quest.IsTracked = false;
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var quest in quests)
        {
            gameEvents.Enqueue(new QuestJournalUpdatedEvent(quest.Quest.Name));
        }
    }
}
