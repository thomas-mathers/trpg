namespace TRPG.Contracts.Scenes.Responses;

public enum EntityType
{
    Creature,
    Building,
    District,
    Item,
    World,
    Country,
    State,
    City,
}

public record NamedEntity(Guid Id, string Name, EntityType Type);
