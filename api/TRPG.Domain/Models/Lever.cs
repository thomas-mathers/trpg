namespace TRPG.Domain.Models;

// Pulling is a remote state change, not a portable item — nothing to lose, nothing to loot off a
// body, and unlike a key it can live on a different route than the gate it opens.
public class Lever : Prop
{
    public bool IsPulled { get; set; }
}
