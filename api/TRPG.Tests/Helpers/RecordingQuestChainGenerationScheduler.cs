using TRPG.Application.LocationSimulation.Commands;

namespace TRPG.Tests.Helpers;

public sealed class RecordingQuestChainGenerationScheduler : IQuestChainGenerationScheduler
{
    public List<GenerateQuestChainCommand> ScheduledCommands { get; } = [];

    public Task ScheduleAsync(
        GenerateQuestChainCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ScheduledCommands.Add(command);
        return Task.CompletedTask;
    }
}
