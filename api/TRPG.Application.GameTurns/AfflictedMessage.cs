namespace TRPG.Application.GameTurns;

internal static class AfflictedMessage
{
    public static string For(string action) =>
        $"You can't {action} while a lingering effect is wearing on you.";
}
