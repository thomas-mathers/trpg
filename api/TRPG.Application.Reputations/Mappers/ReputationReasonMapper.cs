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
            ReputationReason.WitnessedTheft => "Witnessed a theft",
            _ => throw new ArgumentOutOfRangeException(nameof(reason)),
        };
}
