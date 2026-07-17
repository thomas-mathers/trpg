using System.Text.Json;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;
using TRPG.Application.Worlds.Commands;
using TRPG.Contracts;
using TRPG.Data;

namespace TRPG.Jobs;

public class CreateWorldJob(CreateWorldCommandHandler handler, TrpgTickerQDbContext tickerContext)
    : ITickerFunction<CreateWorldCommand>
{
    public async Task ExecuteAsync(
        TickerFunctionContext<CreateWorldCommand> context,
        CancellationToken cancellationToken
    )
    {
        var result = await handler.Handle(context.Request, cancellationToken);

        var ticker = await tickerContext.TimeTickers.FindAsync([context.Id], cancellationToken);
        if (ticker is not null)
        {
            ticker.ResultJson = JsonSerializer.Serialize(
                new CreateWorldResponse(result.WorldId, result.PlayerId, result.WorldName)
            );
            await tickerContext.SaveChangesAsync(cancellationToken);
        }
    }
}
