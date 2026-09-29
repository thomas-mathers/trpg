using TRPG.Domain.Models;

namespace TRPG.Application.CreatureFormulas;

public static class DamageMitigation
{
    public static int Calculate(float amount, float resistance, float maximumResistance) =>
        Math.Max(0, (int)(amount * (1 - Math.Min(maximumResistance, resistance))));

    public static AttributeName ResistanceAttribute(DamageType damageType) =>
        damageType switch
        {
            DamageType.Physical => AttributeName.PhysicalResistance,
            DamageType.Fire => AttributeName.FireResistance,
            DamageType.Ice => AttributeName.IceResistance,
            DamageType.Lightning => AttributeName.LightningResistance,
            DamageType.Poison => AttributeName.PoisonResistance,
            DamageType.Magic => AttributeName.MagicResistance,
        };
}
