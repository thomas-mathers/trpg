namespace TRPG.Models;

internal class Workstation : Prop {
    public Guid? AssignedPersonId { get; set; }
    public Guid? OccupantId { get; set; }
    public WorkstationType WorkstationType { get; init; }
}
