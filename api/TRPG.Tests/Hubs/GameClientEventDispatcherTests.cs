using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Common.Events;
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
