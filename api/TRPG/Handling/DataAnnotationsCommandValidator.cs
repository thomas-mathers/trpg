using System.ComponentModel.DataAnnotations;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Handling;

namespace TRPG.Handling;

internal sealed class DataAnnotationsCommandValidator<TCommand> : ICommandValidator<TCommand>
{
    public Task Validate(TCommand command, CancellationToken cancellationToken = default)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(command!);

        if (!Validator.TryValidateObject(command!, context, results, validateAllProperties: true))
        {
            throw new InputValidationException(results);
        }

        return Task.CompletedTask;
    }
}
