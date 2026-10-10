using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal sealed record CreatureAnchor(Prop Prop, bool IsOccupied);

internal static class CreatureAnchorFinder
{
    internal static CreatureAnchor? Find(Creature creature, IEnumerable<Prop> propsAtLocation)
    {
        var props = propsAtLocation.ToArray();

        var occupied = props.FirstOrDefault(prop => OccupantId(prop) == creature.Id);
        if (occupied is not null)
        {
            return new CreatureAnchor(occupied, IsOccupied: true);
        }

        var assigned = props.FirstOrDefault(prop => prop is Bed && AssignedId(prop) == creature.Id);

        return assigned is null ? null : new CreatureAnchor(assigned, IsOccupied: false);
    }

    private static Guid? OccupantId(Prop prop) =>
        prop switch
        {
            Seat seat => seat.OccupantId,
            Bed bed => bed.OccupantId,
            Workstation workstation => workstation.OccupantId,
            _ => null,
        };

    private static Guid? AssignedId(Prop prop) =>
        prop switch
        {
            Bed bed => bed.AssignedCreatureId,
            _ => null,
        };
}
