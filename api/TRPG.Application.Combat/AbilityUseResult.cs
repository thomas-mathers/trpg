namespace TRPG.Application.Combat;

public enum AbilityUseRejection
{
    NotFound,
    InvalidTarget,
    Unusable,
    GlobalCooldown,
    OutOfRange,
    TooClose,
    NotFacing,
    Incapacitated,
}

public sealed record AbilityUseResult
{
    private AbilityUseResult() { }

    public AbilityUseRejection? Rejection { get; private init; }
    public string? Message { get; private init; }
    public CombatState? State { get; private init; }
    public bool Succeeded => State is not null;

    public static AbilityUseResult Used(CombatState state) => new() { State = state };

    public static AbilityUseResult Rejected(AbilityUseRejection rejection, string message) =>
        new() { Rejection = rejection, Message = message };
}
