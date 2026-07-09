namespace TRPG.Data.Models;

public abstract class Prop
{
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid RoomId { get; init; }
    public Guid WorldId { get; init; }
}
