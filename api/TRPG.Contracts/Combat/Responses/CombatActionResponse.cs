using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Contracts.Combat.Responses;

public record CombatActionResponse(
    CombatUpdatePayload? Update,
    string? ErrorMessage,
    CombatOutcome? Outcome,
    SceneSnapshot? Scene = null
)
{
    public static CombatActionResponse Rejected(string message) => new(null, message, null);
}
