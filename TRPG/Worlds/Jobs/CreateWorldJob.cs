using System.Text.Json;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;
using TRPG.Application.Worlds.Commands;
using TRPG.Contracts;
using TRPG.Contracts.Worlds.Responses;
using TRPG.Data;

namespace TRPG.Worlds.Jobs;

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
                new CreateWorldResponse(result.WorldId, result.PlayerId, result.WorldName),
                TrpgJsonOptions.Default
            );
            await tickerContext.SaveChangesAsync(cancellationToken);
        }
    }
}
