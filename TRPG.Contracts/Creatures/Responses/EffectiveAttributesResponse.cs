namespace TRPG.Contracts.Creatures.Responses;

public record EffectiveAttributesResponse(
    int Strength,
    int Dexterity,
    int Intelligence,
    int Endurance,
    int Stamina,
    int Mana,
    int Defense,
    int MaximumHp,
    int MaximumAp,
    int MaximumMp,
    float MovementSpeed,
    float PhysicalResistance,
    float FireResistance,
    float IceResistance,
    float LightningResistance,
    float PoisonResistance,
    float MagicResistance
);
