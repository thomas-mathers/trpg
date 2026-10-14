using TRPG.Application.Combat;

namespace TRPG.Application.Encounters.Fights;

public sealed class ActiveFight(
    Guid fightId,
    Guid worldId,
    Guid playerId,
    Guid locationId,
    IReadOnlyList<Combatant> combatants
)
{
    public Guid FightId => fightId;
    public Guid WorldId => worldId;
    public Guid PlayerId => playerId;
    public Guid LocationId => locationId;
    public IReadOnlyList<Combatant> Combatants => combatants;
}

public sealed record FightDraft(
    Guid LocationId,
    IReadOnlyList<Combatant> Combatants,
    bool OpensFight,
    bool HasSurpriseRound
);
