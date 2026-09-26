using Microsoft.AspNetCore.SignalR.Client;
using TRPG.Application.Creatures.Events;
using TRPG.Application.Creatures.Results;
using TRPG.Creatures.Responses;
using TRPG.Domain;
using TRPG.GameSessions.Hubs;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Hubs;

public sealed class PlayerVitalsChangedEventMapperTests
{
    [Fact]
    public async Task Map_CallsPlayerVitalsUpdated_WithVitalsAndElapsedGameMilliseconds()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var gameEvent = new PlayerVitalsChangedEvent(
            new CreatureVitals(
                CreatureId: playerId,
                CurrentHp: 12,
                MaximumHp: 40,
                CurrentAp: 3,
                MaximumAp: 10,
                CurrentMp: 5,
                MaximumMp: 8
            ),
            GameClock.Epoch + TimeSpan.FromSeconds(90)
        );
        PlayerVitalsUpdated? received = null;
        var client = new TestGameClient
        {
            Connection = new HubConnectionBuilder().WithUrl("http://localhost").Build(),
            OnPlayerVitalsUpdated = vitals => received = vitals,
        };

        // Act
        await new PlayerVitalsChangedEventMapper().Map(gameEvent).Invoke(client);

        // Assert
        Assert.Equal(
            new PlayerVitalsUpdated(
                PlayerId: playerId,
                CurrentHp: 12,
                MaximumHp: 40,
                CurrentAp: 3,
                MaximumAp: 10,
                CurrentMp: 5,
                MaximumMp: 8,
                GameTimeMilliseconds: 90_000
            ),
            received
        );
    }
}
