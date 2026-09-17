using TickerQ.Utilities;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;
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

// The IQuestChainGenerationScheduler implementation the host wires up — LocationSimulation only
// depends on the interface, never on TickerQ directly. Success/failure is tracked on
// QuestChainGenerationRequest.Status by GenerateQuestChainCommand itself, so this scheduler doesn't
// need to inspect or persist the ticker's own result.
internal class TickerQuestChainGenerationScheduler(ITimeTickerManager<TrpgTimeTicker> timeTicker)
    : IQuestChainGenerationScheduler
{
    public async Task ScheduleAsync(
        GenerateQuestChainCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await timeTicker.AddAsync(
            new TrpgTimeTicker
            {
                Request = TickerHelper.CreateTickerRequest(command),
                ExecutionTime = DateTime.UtcNow,
                Function = TickerFunctionProvider.GetFunctionName<GenerateQuestChainJob>(),
            },
            cancellationToken
        );
    }
}
