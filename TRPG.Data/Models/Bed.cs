namespace TRPG.Data.Models;

public class Bed : Prop
{
    public Guid? AssignedCreatureId { get; set; }
    public Guid? OccupantId { get; set; }
}
