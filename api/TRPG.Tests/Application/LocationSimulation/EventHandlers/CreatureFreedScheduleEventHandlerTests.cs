using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.LocationSimulation.EventHandlers;
using TRPG.Domain;

namespace TRPG.Tests.Application.LocationSimulation.EventHandlers;

public sealed class CreatureFreedScheduleEventHandlerTests
{
    [Fact]
    public async Task Handle_UsesTheCapturedEventTime()
    {
        var recorder = new RecordingScheduleCommandHandler();
        var handler = new CreatureFreedScheduleEventHandler(recorder);
        var creatureId = Guid.NewGuid();
        var gameTime = GameClock.Epoch + TimeSpan.FromHours(17);

        await handler.Handle(
            new CreatureFreedEvent(Guid.NewGuid(), Guid.NewGuid(), creatureId, gameTime),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(gameTime, recorder.Command!.GameTime);
        Assert.Equal(gameTime, recorder.Command.BecameAvailableAtGameTimeByCreatureId[creatureId]);
    }

    private sealed class RecordingScheduleCommandHandler
        : ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
    {
        public SyncCreatureJobSchedulesCommand? Command { get; private set; }

        public Task<SyncCreatureJobSchedulesResult> Handle(
            SyncCreatureJobSchedulesCommand command,
            CancellationToken cancellationToken = default
        )
        {
            Command = command;
            return Task.FromResult(
                new SyncCreatureJobSchedulesResult(new Dictionary<Guid, IReadOnlyList<Guid>>())
            );
        }
    }
}
