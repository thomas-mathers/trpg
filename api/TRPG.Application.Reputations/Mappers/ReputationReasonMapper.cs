using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Mappers;

public static class ReputationReasonMapper
{
    public static string ToDisplayText(this ReputationReason reason) =>
        reason switch
        {
            ReputationReason.QuestCompleted => "Completed a quest",
            ReputationReason.KilledFactionMember => "Killed a local",
            ReputationReason.StoleFromFactionMember => "Witnessed theft",
            ReputationReason.PaidFineToGuard => "Paid a fine",
            ReputationReason.ServedJailTime => "Served jail time",
            ReputationReason.WitnessedKilling => "Witnessed a killing",
            ReputationReason.AssaultedFactionMember => "Assaulted a local",
            ReputationReason.WitnessedAssault => "Witnessed an assault",
            ReputationReason.WitnessedTheft => "Witnessed a theft",
            ReputationReason.PickedFactionLock => "Broke into a property",
            ReputationReason.WitnessedLockpicking => "Witnessed a break-in",
            ReputationReason.TrespassedOnFactionProperty => "Trespassed on a property",
            ReputationReason.WitnessedTrespassing => "Witnessed trespassing",
            ReputationReason.CaughtSneaking => "Caught sneaking",
            ReputationReason.CaughtFleeingSuspicion => "Fled from a guard's questioning",
        };
}
