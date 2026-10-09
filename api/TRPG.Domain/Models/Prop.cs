namespace TRPG.Domain.Models;

public abstract class Prop
{
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid LocationId { get; init; }
    public Guid? OwnerCreatureId { get; set; }
    public Guid WorldId { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Angle { get; set; }
    public Guid? ApproachNodeId { get; set; }
    public double Width { get; set; }
    public double Depth { get; set; }
}
