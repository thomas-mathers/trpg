using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal class GuildHallOccupantGeneratorInput
{
    public required Guid WorldId { get; init; }
    public required Guid CityFactionId { get; init; }
    public required Guid GuildFactionId { get; init; }
    public required Guid GroundFloorLocationId { get; init; }
    public required IReadOnlyList<Bed> Beds { get; init; }
    public required Creature Owner { get; init; }
    public required IReadOnlyList<Creature> Members { get; init; }
}

internal record GuildHallOccupantGeneratorResult(
    IReadOnlyList<FactionMember> FactionMembers,
    IReadOnlyList<CreatureJob> Jobs
);

internal static class GuildHallOccupantGenerator
{
    internal static GuildHallOccupantGeneratorResult Generate(GuildHallOccupantGeneratorInput input)
    {
        var occupants = new List<(Creature Creature, FactionRole Role, bool IsWorker)>
        {
            (input.Owner, FactionRole.Leader, true),
        };
        occupants.AddRange(input.Members.Select(member => (member, FactionRole.Member, false)));

        var factionMembers = new List<FactionMember>();
        var jobs = new List<CreatureJob>();

        foreach (var (occupant, role, isWorker) in occupants)
        {
            factionMembers.Add(
                new FactionMember
                {
                    FactionId = input.GuildFactionId,
                    CreatureId = occupant.Id,
                    Role = role,
                    WorldId = input.WorldId,
                }
            );
            factionMembers.Add(
                new FactionMember
                {
                    FactionId = input.CityFactionId,
                    CreatureId = occupant.Id,
                    Role = FactionRole.Member,
                    WorldId = input.WorldId,
                }
            );

            occupant.LocationId = input.GroundFloorLocationId;

            var bedLocationId = input
                .Beds.First(b => b.AssignedCreatureId == occupant.Id)
                .LocationId;

            jobs.AddRange(
                CreatureJobGenerator.Generate(
                    occupant.Id,
                    bedLocationId,
                    isWorker ? input.GroundFloorLocationId : null,
                    input.GroundFloorLocationId,
                    input.WorldId
                )
            );
        }

        return new GuildHallOccupantGeneratorResult(factionMembers, jobs);
    }
}
