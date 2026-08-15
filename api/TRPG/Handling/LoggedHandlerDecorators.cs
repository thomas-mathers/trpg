using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Handling;

namespace TRPG.Handling;

internal sealed class LoggedCommandHandler<TCommand>(
    ICommandHandler<TCommand> inner,
    ILogger<LoggedCommandHandler<TCommand>> logger
) : ICommandHandler<TCommand>
{
    public async Task Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        logger.LogDebug("Handling {CommandType}", typeof(TCommand).Name);
        try
        {
            await inner.Handle(command, cancellationToken);
            logger.LogDebug(
                "Handled {CommandType} in {ElapsedMilliseconds} ms",
                typeof(TCommand).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Failed {CommandType} after {ElapsedMilliseconds} ms",
                typeof(TCommand).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            throw;
        }
    }
}

internal sealed class LoggedCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<LoggedCommandHandler<TCommand, TResult>> logger
) : ICommandHandler<TCommand, TResult>
{
    public async Task<TResult> Handle(
        TCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var startedAt = Stopwatch.GetTimestamp();
        logger.LogDebug("Handling {CommandType}", typeof(TCommand).Name);
        try
        {
            var result = await inner.Handle(command, cancellationToken);
            logger.LogDebug(
                "Handled {CommandType} in {ElapsedMilliseconds} ms",
                typeof(TCommand).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            return result;
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Failed {CommandType} after {ElapsedMilliseconds} ms",
                typeof(TCommand).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            throw;
        }
    }
}

internal sealed class LoggedQueryHandler<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<LoggedQueryHandler<TQuery, TResult>> logger
) : IQueryHandler<TQuery, TResult>
{
    public async Task<TResult> Handle(TQuery query, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        logger.LogDebug("Handling {QueryType}", typeof(TQuery).Name);
        try
        {
            var result = await inner.Handle(query, cancellationToken);
            logger.LogDebug(
                "Handled {QueryType} in {ElapsedMilliseconds} ms",
                typeof(TQuery).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            return result;
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Failed {QueryType} after {ElapsedMilliseconds} ms",
                typeof(TQuery).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            throw;
        }
    }
}
