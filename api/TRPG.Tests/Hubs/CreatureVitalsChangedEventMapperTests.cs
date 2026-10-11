using Microsoft.AspNetCore.SignalR.Client;
using TRPG.Application.Creatures.Events;
using TRPG.Application.Creatures.Results;
using TRPG.Creatures.Responses;
using TRPG.GameSessions.Hubs;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Hubs;

public sealed class CreatureVitalsChangedEventMapperTests
{
    [Fact]
    public async Task Map_CallsCreatureVitalsUpdated_WithEntriesLocationAndStateVersion()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var creatureId = Guid.NewGuid();
        var gameEvent = new CreatureVitalsChangedEvent(
            worldId,
            locationId,
            [
                new CreatureVitals(
                    CreatureId: creatureId,
                    CurrentHp: 12,
                    MaximumHp: 40,
                    CurrentAp: 3,
                    MaximumAp: 10,
                    CurrentMp: 5,
                    MaximumMp: 8
                ),
            ],
            Version: 42
        );
        CreatureVitalsUpdated? received = null;
        var client = new TestGameClient
        {
            Connection = new HubConnectionBuilder().WithUrl("http://localhost").Build(),
            OnCreatureVitalsUpdated = vitals => received = vitals,
        };

        // Act
        await new CreatureVitalsChangedEventMapper().Map(gameEvent).Invoke(client);

        // Assert
        Assert.NotNull(received);
        Assert.Equal(worldId, received.WorldId);
        Assert.Equal(locationId, received.LocationId);
        Assert.Equal(42, received.Version);
        Assert.Equal(
            new CreatureVitalsEntry(
                CreatureId: creatureId,
                CurrentHp: 12,
                MaximumHp: 40,
                CurrentAp: 3,
                MaximumAp: 10,
                CurrentMp: 5,
                MaximumMp: 8
            ),
            Assert.Single(received.Entries)
        );
    }
}
