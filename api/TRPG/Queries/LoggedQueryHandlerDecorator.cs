using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Queries;

namespace TRPG.Queries;

internal sealed class LoggedQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<LoggedQueryHandlerDecorator<TQuery, TResult>> logger
) : IQueryHandler<TQuery, TResult>
{
    public async Task<TResult> Handle(TQuery query, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        logger.LogTrace("Handling {QueryType}", typeof(TQuery).Name);
        try
        {
            var result = await inner.Handle(query, cancellationToken);
            logger.LogTrace(
                "Handled {QueryType} in {ElapsedMilliseconds} ms",
                typeof(TQuery).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            return result;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed {QueryType} after {ElapsedMilliseconds} ms",
                typeof(TQuery).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            throw;
        }
    }
}
