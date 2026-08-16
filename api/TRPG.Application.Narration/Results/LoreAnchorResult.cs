namespace TRPG.Application.Narration.Results;

public enum LoreAnchorType
{
    Creature,
    Building,
    District,
    World,
    Country,
    State,
    City,
}

public record LoreAnchorResult(
    Guid Id,
    string Name,
    LoreAnchorType Type,
    string? Subtype,
    string Description
);
