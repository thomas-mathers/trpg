using TRPG.Application.Creatures.Events;
using TRPG.Creatures.Responses;
using TRPG.Domain;

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
                GameTimeMilliseconds: (long)(gameEvent.GameTime - GameClock.Epoch).TotalMilliseconds
            ),
            static (client, arguments) => client.PlayerVitalsUpdated(arguments)
        );
}
