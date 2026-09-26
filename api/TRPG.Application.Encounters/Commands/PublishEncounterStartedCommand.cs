using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class PublishEncounterStartedCommand
{
    public required Guid PlayerId { get; init; }
    public required Encounter? Encounter { get; init; }
    public GameInstant GameTime { get; init; } = GameClock.Epoch;
}

internal class PublishEncounterStartedCommandHandler(
    IGameClientEventSink gameEvents,
    IQueryHandler<GetGoldQuantityQuery, int> getGoldQuantity,
    EncounterEngagementManager engagementManager
) : ICommandHandler<PublishEncounterStartedCommand>
{
    public async Task Handle(
        PublishEncounterStartedCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Encounter != null)
        {
            await engagementManager.Engage(command.Encounter, command.GameTime, cancellationToken);
        }

        switch (command.Encounter)
        {
            case HostileEncounter hostileEncounter:
                gameEvents.Enqueue(new HostileEncounterStartedEvent(hostileEncounter));
                break;
            case ShakedownEncounter shakedownEncounter:
                var goldForToll = await getGoldQuantity.Handle(
                    new GetGoldQuantityQuery
                    {
                        Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                    },
                    cancellationToken
                );
                gameEvents.Enqueue(
                    new ShakedownEncounterStartedEvent(
                        shakedownEncounter,
                        goldForToll >= shakedownEncounter.TollAmount
                    )
                );
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
            case TrapEncounter trapEncounter:
                gameEvents.Enqueue(new TrapEncounterStartedEvent(trapEncounter));
                break;
        }
    }
}
