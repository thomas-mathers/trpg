using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class PublishEncounterStartedCommand
{
    public required Guid PlayerId { get; init; }
    public required Encounter? Encounter { get; init; }
}

internal class PublishEncounterStartedCommandHandler(
    IGameClientEventSink gameEvents,
    IQueryHandler<GetGoldQuantityQuery, int> getGoldQuantity
) : ICommandHandler<PublishEncounterStartedCommand>
{
    public async Task Handle(
        PublishEncounterStartedCommand command,
        CancellationToken cancellationToken = default
    )
    {
        switch (command.Encounter)
        {
            case HostileEncounter hostileEncounter:
                gameEvents.Enqueue(new HostileEncounterStartedEvent(hostileEncounter));
                break;
            case GuardEncounter guardEncounter:
                var playerGold = await getGoldQuantity.Handle(
                    new GetGoldQuantityQuery
                    {
                        Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                    },
                    cancellationToken
                );
                gameEvents.Enqueue(
                    new GuardEncounterStartedEvent(
                        guardEncounter,
                        playerGold >= guardEncounter.FineAmount
                    )
                );
                break;
            case TheftEncounter theftEncounter:
                gameEvents.Enqueue(new TheftEncounterStartedEvent(theftEncounter));
                break;
            case SuspicionEncounter suspicionEncounter:
                gameEvents.Enqueue(new SuspicionEncounterStartedEvent(suspicionEncounter));
                break;
        }
    }
}
