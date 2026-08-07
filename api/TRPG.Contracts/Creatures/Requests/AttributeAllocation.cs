namespace TRPG.Contracts.Creatures.Requests;

public record AttributeAllocation
{
    public int Strength { get; init; }
    public int Dexterity { get; init; }
    public int Endurance { get; init; }
    public int Stamina { get; init; }
    public int Mana { get; init; }
    public int Intelligence { get; init; }

    public static AttributeAllocation FromDictionary(
        IReadOnlyDictionary<AllocatableAttributeName, int> values
    ) =>
        new()
        {
            Strength = values.GetValueOrDefault(AllocatableAttributeName.Strength),
            Dexterity = values.GetValueOrDefault(AllocatableAttributeName.Dexterity),
            Endurance = values.GetValueOrDefault(AllocatableAttributeName.Endurance),
            Stamina = values.GetValueOrDefault(AllocatableAttributeName.Stamina),
            Mana = values.GetValueOrDefault(AllocatableAttributeName.Mana),
            Intelligence = values.GetValueOrDefault(AllocatableAttributeName.Intelligence),
        };

    public IReadOnlyDictionary<AllocatableAttributeName, int> ToDictionary() =>
        new Dictionary<AllocatableAttributeName, int>
        {
            [AllocatableAttributeName.Strength] = Strength,
            [AllocatableAttributeName.Dexterity] = Dexterity,
            [AllocatableAttributeName.Endurance] = Endurance,
            [AllocatableAttributeName.Stamina] = Stamina,
            [AllocatableAttributeName.Mana] = Mana,
            [AllocatableAttributeName.Intelligence] = Intelligence,
        };
}
