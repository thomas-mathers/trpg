using TRPG.Application.Creatures.Events;
using TRPG.Creatures.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class CreatureVitalsChangedEventMapper
    : GameClientEventMapper<CreatureVitalsChangedEvent>
{
    protected override IGameClientCall Map(CreatureVitalsChangedEvent gameEvent) =>
        new GameClientCall<CreatureVitalsUpdated>(
            new CreatureVitalsUpdated(
                WorldId: gameEvent.WorldId,
                LocationId: gameEvent.LocationId,
                Entries:
                [
                    .. gameEvent.Vitals.Select(vitals => new CreatureVitalsEntry(
                        CreatureId: vitals.CreatureId,
                        CurrentHp: vitals.CurrentHp,
                        MaximumHp: vitals.MaximumHp,
                        CurrentAp: vitals.CurrentAp,
                        MaximumAp: vitals.MaximumAp,
                        CurrentMp: vitals.CurrentMp,
                        MaximumMp: vitals.MaximumMp
                    )),
                ],
                Version: gameEvent.Version
            ),
            static (client, arguments) => client.CreatureVitalsUpdated(arguments)
        );
}
