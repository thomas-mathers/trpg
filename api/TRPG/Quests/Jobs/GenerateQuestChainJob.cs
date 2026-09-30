using TickerQ.Utilities;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;
using TRPG.Application.Common.Commands;
using TRPG.Application.QuestGeneration.Commands;
using TRPG.Data;

namespace TRPG.Quests.Jobs;

public class GenerateQuestChainJob(ICommandHandler<GenerateQuestChainCommand, bool> handler)
    : ITickerFunction<GenerateQuestChainCommand>
{
    public async Task ExecuteAsync(
        TickerFunctionContext<GenerateQuestChainCommand> context,
        CancellationToken cancellationToken
    ) => await handler.Handle(context.Request, cancellationToken);
}

internal class TickerQuestChainGenerationScheduler(ITimeTickerManager<TrpgTimeTicker> timeTicker)
    : IQuestChainGenerationScheduler
{
    internal static readonly TimeSpan DispatchDelay = TimeSpan.FromSeconds(5);

    public async Task ScheduleAsync(
        GenerateQuestChainCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await timeTicker.AddAsync(
            new TrpgTimeTicker
            {
                Request = TickerHelper.CreateTickerRequest(command),
                // Near-now TickerQ jobs execute inline, before an ambient gameplay transaction commits.
                ExecutionTime = DateTime.UtcNow.Add(DispatchDelay),
                Function = TickerFunctionProvider.GetFunctionName<GenerateQuestChainJob>(),
            },
            cancellationToken
        );
    }
}
