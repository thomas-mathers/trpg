namespace TRPG.Contracts.Scenes.Responses;

public enum EntityType
{
    Creature,
    Building,
    District,
    World,
    Country,
    State,
    City,
    Item,
}

public record LoreAnchor(
    Guid Id,
    string Name,
    EntityType Type,
    string? Subtype,
    string Description
);
