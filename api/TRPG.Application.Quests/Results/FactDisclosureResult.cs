namespace TRPG.Application.Quests.Results;

public enum FactDisclosureOutcome
{
    Blocked,
    TooWeak,
    CannotAfford,
    LockedOut,
    Failed,
    Disclosed,
}

public record DisclosedFact(Guid FactId, string Text);

public record FactDisclosureResult(
    FactDisclosureOutcome Outcome,
    string? FactText = null,
    DisclosedFact? ReasonFact = null
);
