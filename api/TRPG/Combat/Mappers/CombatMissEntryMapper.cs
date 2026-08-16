using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using CombatMissEntry = TRPG.Combat.ClientModels.CombatMissEntry;

namespace TRPG.Combat.Mappers;

internal static class CombatMissEntryMapper
{
    public static CombatMissEntry ToContract(this Miss miss) =>
        new(miss.AttackerId, miss.AttackerName, miss.AbilityName, miss.TargetId, miss.TargetName)
        {
            Narration = CombatNarration.Describe(miss),
        };
}
