namespace TRPG.GameSessions.Responses;

public record LoreAnchor(
    Guid Id,
    string Name,
    EntityType Type,
    string? Subtype,
    string Description
);
