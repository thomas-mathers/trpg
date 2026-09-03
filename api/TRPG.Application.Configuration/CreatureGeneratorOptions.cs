namespace TRPG.Application.Configuration;

public class CreatureGeneratorOptions
{
    public int PointsPerLevel { get; init; } = 5;
    public int SkillExperiencePerAbilityUse { get; init; } = 10;
    public float RestedSkillExperienceMultiplier { get; init; } = 1.25f;
    public int HpPerEndurance { get; init; } = 5;
    public int ApPerStamina { get; init; } = 2;
    public int MpPerMana { get; init; } = 2;
    public int BaseCarryingCapacity { get; init; } = 80;
    public int CarryWeightPerEndurance { get; init; } = 10;
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
