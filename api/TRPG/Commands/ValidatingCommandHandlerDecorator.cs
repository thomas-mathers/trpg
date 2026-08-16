using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Validation;

namespace TRPG.Commands;

internal sealed class ValidatingCommandHandlerDecorator<TCommand>(
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

internal sealed class ValidatingCommandHandlerDecorator<TCommand, TResult>(
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
