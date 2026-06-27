namespace TRPG.Models;

internal class Prop {
    public Rectangle Boundary { get; init; } = null!;
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid RoomId { get; init; }
}
