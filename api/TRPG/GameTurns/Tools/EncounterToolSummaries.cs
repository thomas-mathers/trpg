using TRPG.Domain.Models;

namespace TRPG.GameTurns.Tools;

internal record EncounterMemberSummary(string Name, CreatureType CreatureType, int Level);

internal record HostileEncounterSummary(
    string FactionName,
    string LocationName,
    IReadOnlyCollection<EncounterMemberSummary> Members
);

internal record GuardOffenseSummary(string Description, bool AgainstTheGuard);

internal record GuardEncounterSummary(
    string GuardName,
    string LocationName,
    int FineAmount,
    int JailHours,
    IReadOnlyCollection<GuardOffenseSummary> RecentOffenses
);
