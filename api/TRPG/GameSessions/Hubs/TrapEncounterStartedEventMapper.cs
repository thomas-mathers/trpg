using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;
using DomainTrapKind = TRPG.Domain.Models.TrapKind;

namespace TRPG.GameSessions.Hubs;

internal sealed class TrapEncounterStartedEventMapper
    : GameClientEventMapper<TrapEncounterStartedEvent>
{
    private static readonly string[] MechanicalActions = ["Attempt", "Withdraw", "Disarm"];
    private static readonly string[] NonMechanicalActions = ["Attempt", "Withdraw"];

    protected override IGameClientCall Map(TrapEncounterStartedEvent gameEvent) =>
        new GameClientCall<TrapEncounterState>(
            new TrapEncounterState(
                gameEvent.Encounter.Id,
                (TrapKind)gameEvent.Encounter.TrapKind,
                gameEvent.Encounter.LocationName,
                gameEvent.Encounter.TrapKind == DomainTrapKind.Mechanical
                    ? MechanicalActions
                    : NonMechanicalActions
            ),
            static (client, arguments) => client.TrapEncounterStarted(arguments)
        );
}
