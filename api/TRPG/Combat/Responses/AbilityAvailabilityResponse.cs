namespace TRPG.Combat.Responses;

public record AbilityAvailabilityResponse(string Name, bool IsUsable, string? Reason);
