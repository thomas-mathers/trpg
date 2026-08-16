namespace TRPG.Creatures.Responses;

public record CreatureGenerationOptionsResponse(
    int PointsPerLevel,
    BaseAttributesResponse BaseAttributes
);
