using TRPG.Domain;

namespace TRPG.Application.GameTurns;

public enum ActionFailure
{
    NoEntrance,
    Locked,
    NothingToEnter,
    EncounterActive,
    SeatOccupied,
    SeatNotNearby,
    PlayerNotIdle,
    NotSitting,
    InvalidDuration,
    Afflicted,
    NotYourRoom,
    NothingToActivate,
    AlreadyActivated,
    QuestUnavailable,
    NothingToDeliver,
    NoFight,
    NotDead,
    NoTicket,
    CaravanNotPresent,
    TravelSuspended,
    InsufficientGold,
    AlreadyHoldsTicket,
    InvalidDestination,
    NoEncounter,
    AbilityNotFound,
    AbilityUnavailable,
}

public sealed record ActionOutcome(ActionFailure? Failure, GameInstant? AdvancedGameTime = null)
{
    public static ActionOutcome Success { get; } = new((ActionFailure?)null);

    public bool Succeeded => Failure is null;

    public static ActionOutcome Failed(ActionFailure failure) => new(failure);

    public static ActionOutcome TimeAdvanced(GameInstant gameTime) => new(null, gameTime);
}
