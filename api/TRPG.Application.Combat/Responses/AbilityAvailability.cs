namespace TRPG.Application.Combat.Responses;

public record AbilityAvailability(string Name, bool IsUsable, string? Reason);
