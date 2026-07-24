namespace TRPG.Application.Configuration;

public class CreatureGeneratorOptions
{
    public int PointsPerLevel { get; init; } = 5;
    public int SkillExperiencePerAbilityUse { get; init; } = 10;
    public int HpPerEndurance { get; init; } = 5;
    public int ApPerStamina { get; init; } = 2;
    public int MpPerMana { get; init; } = 2;
    public StartingAttributes BaseAttributes { get; init; } = new();
}

public class StartingAttributes
{
    public int Strength { get; init; } = 1;
    public int Defense { get; init; } = 1;
    public int Dexterity { get; init; } = 1;
    public int Endurance { get; init; } = 1;
    public int Stamina { get; init; } = 1;
    public int Mana { get; init; } = 1;
    public int Intelligence { get; init; } = 1;

    public int Total() =>
        Strength + Defense + Dexterity + Endurance + Stamina + Mana + Intelligence;
}
