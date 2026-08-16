using TRPG.Application.Combat;
using TRPG.Combat.Requests;

namespace TRPG.Combat.Mappers;

internal static class CombatActionRequestMapper
{
    public static PlayerCombatAction ToAction(this CombatActionRequest request) =>
        request switch
        {
            UseAbilityCombatActionRequest action => new UseAbilityAction(
                action.TargetId,
                action.AbilityName
            ),
            UseItemCombatActionRequest action => new UseItemAction(action.ItemName),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
}
