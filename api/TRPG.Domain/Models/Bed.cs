namespace TRPG.Domain.Models;

public class Bed : Prop
{
    public Guid? AssignedCreatureId { get; set; }
    public Guid? OccupantId { get; set; }
}
