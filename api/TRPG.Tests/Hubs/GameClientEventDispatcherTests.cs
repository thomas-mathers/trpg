using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Subscriptions;
using TRPG.Domain;
using TRPG.GameSessions.Hubs;

namespace TRPG.Tests.Hubs;

public sealed class GameClientEventDispatcherTests
{
    [Fact]
    public async Task FlushAsync_SendsOneWorldsEventsSequentially_WhenFlushesOverlap()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var registry = new GameClientEventQueueRegistry();
        var firstSendStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var releaseFirstSend = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var secondSendStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var dispatcher = new GameClientEventDispatcher(
            registry,
            new TestHubContext(),
            [new TestEventMapper(firstSendStarted, releaseFirstSend, secondSendStarted)],
            new SceneEventProjector(
                new SceneSubscriptions(
                    Options.Create(new WorldSimulationOptions()),
                    TimeProvider.System
                )
            ),
            NullLogger<GameClientEventDispatcher>.Instance
        );
        registry.Enqueue(new TestEvent(worldId, 1));

        // Act
        var firstFlush = dispatcher.FlushAsync(worldId);
        await firstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        registry.Enqueue(new TestEvent(worldId, 2));
        var secondFlush = dispatcher.FlushAsync(worldId);
        var secondStartedBeforeFirstFinished = secondSendStarted.Task.IsCompleted;
        releaseFirstSend.SetResult();
        await Task.WhenAll(firstFlush, secondFlush).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(secondStartedBeforeFirstFinished);
        Assert.True(secondSendStarted.Task.IsCompleted);
    }

    [Fact]
    public async Task FlushAsync_MarksWatcherForResyncWithoutThrowing_WhenSceneEventSendFails()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var subscriptions = MakeSubscriptions();
        subscriptions.Watch(worldId, playerId, locationId);
        var registry = new GameClientEventQueueRegistry();
        var dispatcher = MakeDispatcher(registry, subscriptions, new ThrowingWeatherMapper());
        registry.Enqueue(MakeWeatherEvent(worldId, locationId));

        // Act
        await dispatcher.FlushAsync(worldId);

        // Assert
        Assert.Equal(ResyncState.Pending, subscriptions.StateOf(worldId, playerId));
    }

    [Fact]
    public async Task FlushAsync_Throws_WhenNonSceneEventSendFails()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var registry = new GameClientEventQueueRegistry();
        var dispatcher = MakeDispatcher(registry, MakeSubscriptions(), new ThrowingTestMapper());
        registry.Enqueue(new TestEvent(worldId, 1));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.FlushAsync(worldId));
    }

    [Fact]
    public async Task FlushAsync_SendsNothing_WhenNobodyWatchesTheSceneEventLocation()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var registry = new GameClientEventQueueRegistry();
        var dispatcher = MakeDispatcher(registry, MakeSubscriptions(), new ThrowingWeatherMapper());
        registry.Enqueue(MakeWeatherEvent(worldId, Guid.NewGuid()));

        // Act
        var sent = await dispatcher.FlushAsync(worldId);

        // Assert
        Assert.False(sent);
    }

    private static SceneSubscriptions MakeSubscriptions() =>
        new(Options.Create(new WorldSimulationOptions()), TimeProvider.System);

    private static GameClientEventDispatcher MakeDispatcher(
        GameClientEventQueueRegistry registry,
        SceneSubscriptions subscriptions,
        IGameClientEventMapper mapper
    ) =>
        new(
            registry,
            new TestHubContext(),
            [mapper],
            new SceneEventProjector(subscriptions),
            NullLogger<GameClientEventDispatcher>.Instance
        );

    private static WeatherChangedEvent MakeWeatherEvent(Guid worldId, Guid locationId) =>
        new(
            worldId,
            locationId,
            new WorldStateStamp(1, GameClock.Epoch, DateTimeOffset.UnixEpoch, 1),
            WeatherCondition: null
        );

    private sealed class ThrowingWeatherMapper : GameClientEventMapper<WeatherChangedEvent>
    {
        protected override IGameClientCall Map(WeatherChangedEvent gameEvent) =>
            new GameClientCall(_ => throw new InvalidOperationException("send failed"));
    }

    private sealed class ThrowingTestMapper : GameClientEventMapper<TestEvent>
    {
        protected override IGameClientCall Map(TestEvent gameEvent) =>
            new GameClientCall(_ => throw new InvalidOperationException("send failed"));
    }

    private sealed record TestEvent(Guid WorldId, int Sequence) : GameClientEvent(WorldId);

    private sealed class TestEventMapper(
        TaskCompletionSource firstSendStarted,
        TaskCompletionSource releaseFirstSend,
        TaskCompletionSource secondSendStarted
    ) : GameClientEventMapper<TestEvent>
    {
        protected override IGameClientCall Map(TestEvent gameEvent) =>
            new GameClientCall(_ => Send(gameEvent.Sequence));

        private async Task Send(int sequence)
        {
            if (sequence == 1)
            {
                firstSendStarted.SetResult();
                await releaseFirstSend.Task;
            }
            else
            {
                secondSendStarted.SetResult();
            }
        }
    }

    private sealed class TestHubContext : IHubContext<ChatHub, IGameClient>
    {
        public IHubClients<IGameClient> Clients { get; } = new TestHubClients();
        public IGroupManager Groups => null!;
    }

    private sealed class TestHubClients : IHubClients<IGameClient>
    {
        public IGameClient All => null!;

        public IGameClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => null!;

        public IGameClient Client(string connectionId) => null!;

        public IGameClient Clients(IReadOnlyList<string> connectionIds) => null!;

        public IGameClient Group(string groupName) => null!;

        public IGameClient GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds
        ) => null!;

        public IGameClient Groups(IReadOnlyList<string> groupNames) => null!;

        public IGameClient User(string userId) => null!;

        public IGameClient Users(IReadOnlyList<string> userIds) => null!;
    }
}
