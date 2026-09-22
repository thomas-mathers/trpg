namespace TRPG.Application.Configuration;

public class CreatureGeneratorOptions
{
    // Shared with CountryPatrolOptions.SpeedUnitsPerHour (ordinary walking pace) and
    // CaravanOptions.SpeedUnitsPerHour (a multiple of it), so every overworld travel speed
    // derives from this one baseline instead of independently duplicating its value.
    public const float WalkingSpeedUnitsPerHour = 50f;

    public int PointsPerLevel { get; init; } = 5;
    public int SkillExperiencePerAbilityUse { get; init; } = 10;
    public float RestedSkillExperienceMultiplier { get; init; } = 1.25f;
    public int HpPerEndurance { get; init; } = 5;
    public int ApPerStamina { get; init; } = 2;
    public int MpPerMana { get; init; } = 2;
    public int BaseCarryingCapacity { get; init; } = 80;
    public int CarryWeightPerEndurance { get; init; } = 10;
    public float BaseMovementSpeed { get; init; } = WalkingSpeedUnitsPerHour;
    public float MovementSpeedPerDexterity { get; init; } = 1f;
    public int MovementSpeedDexterityCap { get; init; } = 20;
    public float ClothMovementPenalty { get; init; } = 0f;
    public float LeatherMovementPenalty { get; init; } = 2f;
    public float MailMovementPenalty { get; init; } = 5f;
    public float PlateMovementPenalty { get; init; } = 10f;
    public float SneakSpeedMultiplier { get; init; } = 0.5f;
    public float MinimumMovementSpeed { get; init; } = 10f;
    public StartingAttributes BaseAttributes { get; init; } = new();
}

public class StartingAttributes
{
    public int Strength { get; init; } = 5;
    public int Defense { get; init; } = 5;
    public int Dexterity { get; init; } = 5;
    public int Endurance { get; init; } = 5;
    public int Stamina { get; init; } = 5;
    public int Mana { get; init; } = 5;
    public int Intelligence { get; init; } = 5;

    public int Total() =>
        Strength + Defense + Dexterity + Endurance + Stamina + Mana + Intelligence;
}
