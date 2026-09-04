using TRPG.Application.Combat;
using TRPG.Application.Configuration;

namespace TRPG.Application.Encounters;

public enum HostileEncounterActionKind
{
    Attack,
    Evade,
    Retreat,
}

internal enum HostileEncounterActionOutcome
{
    Attacked,
    Evaded,
    EvadeFailed,
    Retreated,
    RetreatFailed,
}

internal static class HostileEncounterActionResolver
{
    // roll [0,1) is caller-supplied rather than rolled internally, so this stays pure and testable.
    public static HostileEncounterActionOutcome Resolve(
        HostileEncounterActionKind action,
        FleeOptions fleeOptions,
        EvadeParticipant player,
        IReadOnlyCollection<EvadeParticipant> groupMembers,
        double roll
    ) =>
        action switch
        {
            HostileEncounterActionKind.Attack => HostileEncounterActionOutcome.Attacked,
            HostileEncounterActionKind.Evade => IsCaught(fleeOptions, player, groupMembers, roll)
                ? HostileEncounterActionOutcome.EvadeFailed
                : HostileEncounterActionOutcome.Evaded,
            HostileEncounterActionKind.Retreat => IsCaught(fleeOptions, player, groupMembers, roll)
                ? HostileEncounterActionOutcome.RetreatFailed
                : HostileEncounterActionOutcome.Retreated,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private static bool IsCaught(
        FleeOptions fleeOptions,
        EvadeParticipant player,
        IReadOnlyCollection<EvadeParticipant> groupMembers,
        double roll
    ) => roll < EvadeChanceCalculator.CatchChance(fleeOptions, player, groupMembers);
}
