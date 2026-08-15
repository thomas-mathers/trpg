using System.ComponentModel.DataAnnotations;

namespace TRPG.Application.Common.Exceptions;

public sealed class InputValidationException(IReadOnlyCollection<ValidationResult> errors)
    : Exception(errors.FirstOrDefault()?.ErrorMessage ?? "Input validation failed.")
{
    public IReadOnlyCollection<ValidationResult> Errors { get; } = errors.ToArray();
}
