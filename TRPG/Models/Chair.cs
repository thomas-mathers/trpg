namespace TRPG.Models;

internal class Chair : Prop {
    public Guid? OccupantId { get; set; }
    public Guid? TableId { get; init; }
}
