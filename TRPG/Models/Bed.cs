namespace TRPG.Models;

internal class Bed : Prop {
    public Guid? AssignedPersonId { get; set; }
    public Guid? OccupantId { get; set; }
}