using System.ComponentModel.DataAnnotations;

namespace TRPG.Application.Common.Handling;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotBlankAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string text && !string.IsNullOrWhiteSpace(text);
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is Guid guid && guid != Guid.Empty;
}
