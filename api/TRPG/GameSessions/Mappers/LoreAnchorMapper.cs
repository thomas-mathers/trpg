using TRPG.Application.Narration.Queries;
using TRPG.Contracts.Narration.Responses;
using TRPG.Contracts.Scenes.Responses;
using TRPG.Domain.Models;

namespace TRPG.GameSessions.Mappers;

internal static class LoreAnchorMapper
{
    public static LoreAnchor ToContract(this LoreAnchorSummary anchor) =>
        new(
            anchor.Id,
            anchor.Name,
            anchor.Type switch
            {
                LoreAnchorType.Creature => EntityType.Creature,
                LoreAnchorType.Building => EntityType.Building,
                LoreAnchorType.District => EntityType.District,
                LoreAnchorType.World => EntityType.World,
                LoreAnchorType.Country => EntityType.Country,
                LoreAnchorType.State => EntityType.State,
                LoreAnchorType.City => EntityType.City,
                _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
            },
            anchor.Subtype,
            anchor.Description
        );
}
