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
    // Matches appsettings.json's "CreatureGenerator:BaseAttributes" section - kept in sync so
    // that code relying on the C# default (e.g. TRPG.Balance's experiments, which never wire up
    // configuration) matches actual production values instead of silently diverging from them.
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
