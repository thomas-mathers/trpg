using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat;

public record CombatState(
    CombatOutcome Outcome,
    IReadOnlyList<CombatantResult> Combatants,
    IReadOnlyList<CombatResolution> Events,
    IReadOnlyDictionary<WeaponType, int> WeaponSwingCounts,
    IReadOnlyDictionary<Skill, int> SkillUsageCounts
);
