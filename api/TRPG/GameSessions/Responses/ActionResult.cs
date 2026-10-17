using Tapper;
using TRPG.Application.GameTurns;

namespace TRPG.GameSessions.Responses;

[TranspilationSource]
public enum ActionFailureReason
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

[TranspilationSource]
public record ActionResult(bool Succeeded, ActionFailureReason? Reason, string? Detail = null)
{
    internal static ActionResult From(ActionOutcome outcome) =>
        new(
            outcome.Succeeded,
            outcome.Failure is { } failure ? ToReason(failure) : null,
            outcome.Detail
        );

    private static ActionFailureReason ToReason(ActionFailure failure) =>
        Enum.Parse<ActionFailureReason>(failure.ToString());
}
