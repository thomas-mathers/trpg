using TRPG.Application.Combat;
using TRPG.Domain.Models;

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
    public IReadOnlyList<Guid> CombatantIds =>
        [.. combatants.Select(combatant => combatant.CreatureId)];

    public bool HasEnemy(Guid creatureId) =>
        combatants.Any(combatant => combatant.CreatureId == creatureId && !combatant.IsPlayer);

    public Point PositionOf(Guid creatureId) =>
        combatants.Single(combatant => combatant.CreatureId == creatureId).Position;
}

public sealed record FightDraft(
    Guid LocationId,
    IReadOnlyList<Combatant> Combatants,
    bool OpensFight,
    bool HasSurpriseRound
);
