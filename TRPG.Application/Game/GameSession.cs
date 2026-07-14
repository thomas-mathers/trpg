using TRPG.Application.Combat;

namespace TRPG.Application.Game;

public record GameSession(Guid WorldId, Guid PlayerId, TimeSpan BankedPlaytime)
{
    public Dictionary<string, Guid> OpenConversationCreatureIdsByName { get; } = [];
    public TimeSpan BankedPlaytime { get; set; } = BankedPlaytime;
    public bool DidMoveThisTurn { get; set; }
    public bool DidSceneRefreshThisTurn { get; set; }
    public IReadOnlyList<Combatant>? Combatants { get; set; }
}
