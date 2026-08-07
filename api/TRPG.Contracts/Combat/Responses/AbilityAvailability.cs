namespace TRPG.Contracts.Combat.Responses;

public record AbilityAvailability(string Name, bool IsUsable, string? Reason);
