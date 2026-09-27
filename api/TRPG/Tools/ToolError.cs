using System.Text.Json;

namespace TRPG.Tools;

internal record ToolError(string Error);

internal static class ToolResultOutcome
{
    private const string ErrorPropertyName = "error";

    // The function factory serializes a tool's return value, so a refusal arrives as `{"error": ...}`.
    public static bool IsRejected(object? result) =>
        result is ToolError
        || result is JsonElement { ValueKind: JsonValueKind.Object } element
            && element.EnumerateObject().ToArray()
                is [{ Name: ErrorPropertyName, Value.ValueKind: JsonValueKind.String }];
}
