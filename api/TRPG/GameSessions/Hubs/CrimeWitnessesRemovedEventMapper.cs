using TRPG.Application.Reputations.Events;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class CrimeWitnessesRemovedEventMapper
    : GameClientEventMapper<CrimeWitnessesRemovedEvent>
{
    protected override IGameClientCall Map(CrimeWitnessesRemovedEvent gameEvent) =>
        new GameClientCall<CrimeNotification>(
            new CrimeNotification(gameEvent.CrimeKind.ToString().ToLowerInvariant()),
            static (client, arguments) => client.CrimeWitnessesRemoved(arguments)
        );
}
