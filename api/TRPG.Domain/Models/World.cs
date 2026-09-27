namespace TRPG.Domain.Models;

public class World
{
    public Rectangle Boundary { get; init; } = null!;
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid? PlayerId { get; set; }
    public GameInstant GameTime { get; set; } = GameClock.Epoch;
    public long StateVersion { get; set; }
}
