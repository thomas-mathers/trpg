using TRPG.Application.Common.Mappers;
using TRPG.Contracts.Encounters.Responses;
using TRPG.Data.Models;

namespace TRPG.Application.Encounters;

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
                .Select(member => new HostileEncounterMemberState(
                    member.Name,
                    member.CreatureType.ToContract(),
                    member.Level
                ))
                .ToArray(),
            AllowedActions: AllowedActions
        );
}
