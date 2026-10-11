using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.QuestGeneration.Commands;
using TRPG.Data;
using TRPG.Jobs;

namespace TRPG.Quests.Jobs;

internal sealed class GenerateQuestChainJob(
    ICommandHandler<GenerateQuestChainCommand, bool> handler
)
{
    public async Task Run(GenerateQuestChainCommand command, CancellationToken cancellationToken)
    {
        // Inserts quest rows that no runner decision reads, and holds an LLM call no world request may wait on.
        using var writeScope = WorldWriteScope.Enter();
        await handler.Handle(command, cancellationToken);
    }
}

internal sealed class JobRunnerQuestChainGenerationScheduler(JobRunner jobRunner)
    : IQuestChainGenerationScheduler
{
    public Task ScheduleAsync(
        GenerateQuestChainCommand command,
        CancellationToken cancellationToken = default
    )
    {
        jobRunner.Schedule(
            (services, token) =>
                services.GetRequiredService<GenerateQuestChainJob>().Run(command, token)
        );
        return Task.CompletedTask;
    }
}
