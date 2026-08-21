namespace TRPG.Domain.Models;

public abstract class Prop
{
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid LocationId { get; init; }
    public Guid? OwnerCreatureId { get; set; }
    public Guid WorldId { get; init; }
}
