using System.ComponentModel.DataAnnotations;

namespace TRPG.Application.Common.Exceptions;

internal sealed class InputValidationException(IReadOnlyCollection<ValidationResult> errors)
    : ValidationException(errors.FirstOrDefault()?.ErrorMessage ?? "Input validation failed.")
{
    public IReadOnlyCollection<ValidationResult> Errors { get; } = errors.ToArray();
}
