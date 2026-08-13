using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Contracts.Narration.Responses;

public record LoreAnchor(
    Guid Id,
    string Name,
    EntityType Type,
    string? Subtype,
    string Description
);
