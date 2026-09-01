using TRPG.Application.Crimes.Events;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class CrimeWitnessedEventMapper : GameClientEventMapper<CrimeWitnessedEvent>
{
    protected override IGameClientCall Map(CrimeWitnessedEvent gameEvent) =>
        new GameClientCall<CrimeNotification>(
            new CrimeNotification(gameEvent.CrimeKind.ToString().ToLowerInvariant()),
            static (client, arguments) => client.CrimeWitnessed(arguments)
        );
}
