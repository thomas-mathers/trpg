using TRPG.Data.Models;

namespace TRPG.Application.Encounters.Mappers;

internal static class HostileEncounterStateMapper
{
    private static readonly string[] AllowedActions = ["Attack", "Evade", "Retreat"];

    public static HostileEncounterState ToState(
        Guid encounterId,
        Faction faction,
        IReadOnlyList<Creature> livingMembers,
        Location location
    ) =>
        new(
            EncounterId: encounterId,
            FactionName: faction.Name,
            LocationName: location.Name,
            Members: livingMembers
                .Select(member => new HostileEncounterMember(
                    member.Name,
                    member.CreatureType,
                    member.Level
                ))
                .ToArray(),
            AllowedActions: AllowedActions
        );
}
