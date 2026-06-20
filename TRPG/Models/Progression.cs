namespace TRPG.Models;

public class Progression
{
    public int Level { get; set; }
    public Meter Experience { get; set; } = null!;
}
