namespace TRPG.Contracts.Creatures.Responses;

public record BaseAttributesResponse(
    int Strength,
    int Defense,
    int Dexterity,
    int Endurance,
    int Stamina,
    int Mana,
    int Intelligence
);
