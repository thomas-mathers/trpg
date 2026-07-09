namespace TRPG.Models;

internal class Bed : Prop
{
    public Guid? AssignedCreatureId { get; set; }
    public Guid? OccupantId { get; set; }
}
