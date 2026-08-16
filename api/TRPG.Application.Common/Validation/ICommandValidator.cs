namespace TRPG.Application.Common.Validation;

public interface ICommandValidator<in TCommand>
{
    Task Validate(TCommand command, CancellationToken cancellationToken = default);
}
