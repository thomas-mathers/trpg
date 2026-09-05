using TRPG.Application.Combat;
using TRPG.Application.Configuration;

namespace TRPG.Application.Encounters;

internal static class HostileEncounterActionResolver
{
    // roll [0,1) is caller-supplied rather than rolled internally, so this stays pure and testable.
    public static HostileEncounterResolutionOutcome Resolve(
        HostileEncounterAction action,
        FleeOptions fleeOptions,
        EvadeParticipant player,
        IReadOnlyCollection<EvadeParticipant> groupMembers,
        double roll
    ) =>
        action switch
        {
            AttackEncounterAction => HostileEncounterResolutionOutcome.Attacked,
            EvadeEncounterAction => IsCaught(fleeOptions, player, groupMembers, roll)
                ? HostileEncounterResolutionOutcome.EvadeFailed
                : HostileEncounterResolutionOutcome.Evaded,
            RetreatEncounterAction => IsCaught(fleeOptions, player, groupMembers, roll)
                ? HostileEncounterResolutionOutcome.RetreatFailed
                : HostileEncounterResolutionOutcome.Retreated,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private static bool IsCaught(
        FleeOptions fleeOptions,
        EvadeParticipant player,
        IReadOnlyCollection<EvadeParticipant> groupMembers,
        double roll
    ) => roll < EvadeChanceCalculator.CatchChance(fleeOptions, player, groupMembers);
}
