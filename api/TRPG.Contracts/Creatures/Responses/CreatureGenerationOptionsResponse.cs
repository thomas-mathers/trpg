namespace TRPG.Contracts.Creatures.Responses;

public record CreatureGenerationOptionsResponse(
    int PointsPerLevel,
    BaseAttributesResponse BaseAttributes
);
