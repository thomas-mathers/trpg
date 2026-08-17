using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;

namespace TRPG.Commands;

internal sealed class LoggedCommandHandlerDecorator<TCommand>(
    ICommandHandler<TCommand> inner,
    ILogger<LoggedCommandHandlerDecorator<TCommand>> logger
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
            logger.LogError(
                exception,
                "Failed {CommandType} after {ElapsedMilliseconds} ms",
                typeof(TCommand).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            throw;
        }
    }
}

internal sealed class LoggedCommandHandlerDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<LoggedCommandHandlerDecorator<TCommand, TResult>> logger
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
            logger.LogError(
                exception,
                "Failed {CommandType} after {ElapsedMilliseconds} ms",
                typeof(TCommand).Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            throw;
        }
    }
}
