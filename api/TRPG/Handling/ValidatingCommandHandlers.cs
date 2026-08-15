using TRPG.Application.Common.Handling;

namespace TRPG.Handling;

internal sealed class ValidatingCommandHandler<TCommand>(
    IEnumerable<ICommandValidator<TCommand>> validators,
    ICommandHandler<TCommand> inner
) : ICommandHandler<TCommand>
{
    public async Task Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        foreach (var validator in validators)
        {
            await validator.Validate(command, cancellationToken);
        }

        await inner.Handle(command, cancellationToken);
    }
}

internal sealed class ValidatingCommandHandler<TCommand, TResult>(
    IEnumerable<ICommandValidator<TCommand>> validators,
    ICommandHandler<TCommand, TResult> inner
) : ICommandHandler<TCommand, TResult>
{
    public async Task<TResult> Handle(
        TCommand command,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var validator in validators)
        {
            await validator.Validate(command, cancellationToken);
        }

        return await inner.Handle(command, cancellationToken);
    }
}
