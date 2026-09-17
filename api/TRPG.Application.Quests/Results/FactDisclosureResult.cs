namespace TRPG.Application.Quests.Results;

public enum FactDisclosureOutcome
{
    Blocked,
    TooWeak,
    LockedOut,
    Failed,
    Disclosed,
}

public record FactDisclosureResult(
    FactDisclosureOutcome Outcome,
    string? FactText = null,
    IReadOnlyCollection<string>? MissingRequiredQuestNames = null
);
