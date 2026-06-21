namespace TRPG.Models;

internal class Progression
{
    public int Level { get; set; }
    public Meter Experience { get; set; } = null!;
}
