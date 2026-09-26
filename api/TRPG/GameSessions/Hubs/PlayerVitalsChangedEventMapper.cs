using TRPG.Application.Creatures.Events;
using TRPG.Creatures.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class PlayerVitalsChangedEventMapper
    : GameClientEventMapper<PlayerVitalsChangedEvent>
{
    protected override IGameClientCall Map(PlayerVitalsChangedEvent gameEvent) =>
        new GameClientCall<PlayerVitalsUpdated>(
            new PlayerVitalsUpdated(
                PlayerId: gameEvent.Vitals.CreatureId,
                CurrentHp: gameEvent.Vitals.CurrentHp,
                MaximumHp: gameEvent.Vitals.MaximumHp,
                CurrentAp: gameEvent.Vitals.CurrentAp,
                MaximumAp: gameEvent.Vitals.MaximumAp,
                CurrentMp: gameEvent.Vitals.CurrentMp,
                MaximumMp: gameEvent.Vitals.MaximumMp,
                Version: gameEvent.Version
            ),
            static (client, arguments) => client.PlayerVitalsUpdated(arguments)
        );
}
