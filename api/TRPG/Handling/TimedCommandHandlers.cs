using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Handling;

namespace TRPG.Handling;

internal sealed class TimedCommandHandler<TCommand>(
    ICommandHandler<TCommand> inner,
    ILogger<TimedCommandHandler<TCommand>> logger
) : ICommandHandler<TCommand>
{
    public async Task Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await inner.Handle(command, cancellationToken);
        }
        finally
        {
            logger.LogDebug(
                "Handled {CommandType} in {ElapsedMilliseconds} ms",
                typeof(TCommand).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
        }
    }
}

internal sealed class TimedCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<TimedCommandHandler<TCommand, TResult>> logger
) : ICommandHandler<TCommand, TResult>
{
    public async Task<TResult> Handle(
        TCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            return await inner.Handle(command, cancellationToken);
        }
        finally
        {
            logger.LogDebug(
                "Handled {CommandType} in {ElapsedMilliseconds} ms",
                typeof(TCommand).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
        }
    }
}

internal sealed class TimedQueryHandler<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<TimedQueryHandler<TQuery, TResult>> logger
) : IQueryHandler<TQuery, TResult>
{
    public async Task<TResult> Handle(TQuery query, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            return await inner.Handle(query, cancellationToken);
        }
        finally
        {
            logger.LogDebug(
                "Handled {QueryType} in {ElapsedMilliseconds} ms",
                typeof(TQuery).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
        }
    }
}
