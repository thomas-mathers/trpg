using TRPG.Data.Models;

namespace TRPG.Application.Combat;

// Only the fastest weapon type (Dagger, AttackSpeed 10) grants a bonus swing per turn today -
// every other weapon acts once, matching CombatEngine's normal one-action-per-turn resolution.
public static class WeaponAttackSpeed
{
    private const int FastWeaponAttackSpeedThreshold = 10;

    public static int AttacksPerTurn(Weapon? weapon) =>
        weapon != null && weapon.AttackSpeed >= FastWeaponAttackSpeedThreshold ? 2 : 1;
}
