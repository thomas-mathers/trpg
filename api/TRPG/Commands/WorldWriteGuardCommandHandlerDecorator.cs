using System.Reflection;
using TRPG.Application.Common.Commands;

namespace TRPG.Commands;

internal static class WorldWriteGuard
{
    public static void EnsureAllowed<TCommand>()
    {
        if (Exempt<TCommand>.Value || WorldWriteScope.IsOpen)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{typeof(TCommand).Name} mutates world state and must run inside a world simulation request."
        );
    }

    private static class Exempt<TCommand>
    {
        public static readonly bool Value =
            typeof(TCommand).GetCustomAttribute<ExemptFromWorldWriteGuardAttribute>() != null;
    }
}

internal sealed class WorldWriteGuardCommandHandlerDecorator<TCommand>(
    ICommandHandler<TCommand> inner
) : ICommandHandler<TCommand>
{
    public Task Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        WorldWriteGuard.EnsureAllowed<TCommand>();

        return inner.Handle(command, cancellationToken);
    }
}

internal sealed class WorldWriteGuardCommandHandlerDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner
) : ICommandHandler<TCommand, TResult>
{
    public Task<TResult> Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        WorldWriteGuard.EnsureAllowed<TCommand>();

        return inner.Handle(command, cancellationToken);
    }
}
