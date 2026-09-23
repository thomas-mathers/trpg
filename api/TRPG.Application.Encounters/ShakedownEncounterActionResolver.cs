using TRPG.Application.Combat;
using TRPG.Application.Configuration;

namespace TRPG.Application.Encounters;

internal record ShakedownEncounterResolutionContext(
    FleeOptions FleeOptions,
    IntimidationOptions IntimidationOptions,
    EvadeParticipant Player,
    IReadOnlyCollection<EvadeParticipant> GroupMembers,
    IntimidationParticipant Intimidator,
    IReadOnlyCollection<IntimidationParticipant> IntimidationTargets
);

internal static class ShakedownEncounterActionResolver
{
    // roll [0,1) is caller-supplied rather than rolled internally, so this stays pure and testable.
    public static ShakedownEncounterResolutionOutcome Resolve(
        ShakedownEncounterAction action,
        ShakedownEncounterResolutionContext context,
        double roll
    ) =>
        action switch
        {
            PayTollEncounterAction => ShakedownEncounterResolutionOutcome.PaidToll,
            FightEncounterAction => ShakedownEncounterResolutionOutcome.Fought,
            IntimidateEncounterAction => IsIntimidated(context, roll)
                ? ShakedownEncounterResolutionOutcome.Intimidated
                : ShakedownEncounterResolutionOutcome.IntimidateFailed,
            FleeShakedownEncounterAction => IsCaught(context, roll)
                ? ShakedownEncounterResolutionOutcome.FleeFailed
                : ShakedownEncounterResolutionOutcome.Fled,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private static bool IsIntimidated(ShakedownEncounterResolutionContext context, double roll) =>
        roll
        < IntimidationChanceCalculator.SuccessChance(
            context.IntimidationOptions,
            context.Intimidator,
            context.IntimidationTargets
        );

    private static bool IsCaught(ShakedownEncounterResolutionContext context, double roll) =>
        roll
        < EvadeChanceCalculator.CatchChance(
            context.FleeOptions,
            context.Player,
            context.GroupMembers
        );
}
