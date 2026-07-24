namespace TRPG.Contracts.Creatures.Requests;

public enum AllocatableAttributeName
{
    Strength,
    Dexterity,
    Endurance,
    Stamina,
    Mana,
    Intelligence,
}

public record AllocateAttributePointsRequest(
    IReadOnlyDictionary<AllocatableAttributeName, int> Deltas
);
