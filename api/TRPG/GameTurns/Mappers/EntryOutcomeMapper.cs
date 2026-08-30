using TRPG.Application.GameTurns;
using TRPG.Tools;

namespace TRPG.GameTurns.Mappers;

internal static class EntryOutcomeMapper
{
    public static ToolError? ToToolError(this EntryOutcome outcome, string destinationName) =>
        outcome switch
        {
            EntryOutcome.Entered => null,
            EntryOutcome.NoEntrance => new ToolError(
                $"'{destinationName}' has no entrance. Call look to see what's around."
            ),
            EntryOutcome.Locked => new ToolError($"The door to '{destinationName}' is locked."),
            EntryOutcome.DestinationNotFound => new ToolError(
                $"No building or district named '{destinationName}' found nearby. Call look to see what's around."
            ),
            EntryOutcome.ExitNotFound => new ToolError(
                $"No exit named '{destinationName}' found here. Call look to see the available exits."
            ),
            EntryOutcome.EncounterActive => new ToolError(
                "A hostile encounter is already underway — resolve it before moving."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
}
