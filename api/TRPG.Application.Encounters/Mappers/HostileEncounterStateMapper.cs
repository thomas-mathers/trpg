using TRPG.Data.Models;

namespace TRPG.Application.Encounters.Mappers;

internal static class HostileEncounterStateMapper
{
    private static readonly string[] AllowedActions = ["Attack", "Evade", "Retreat"];

    public static HostileEncounterState ToState(
        this Faction faction,
        Guid encounterId,
        IReadOnlyList<Creature> livingMembers,
        Location location
    ) =>
        new(
            EncounterId: encounterId,
            FactionName: faction.Name,
            LocationName: location.Name,
            Members: livingMembers.Select(member => member.ToEncounterMember()).ToArray(),
            AllowedActions: AllowedActions
        );
}
