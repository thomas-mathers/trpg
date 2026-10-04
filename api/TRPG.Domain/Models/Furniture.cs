namespace TRPG.Domain.Models;

// Pure decor with no gameplay behavior; its look comes from the persisted Model.
public class Furniture : Prop
{
    public required PropModel Model { get; init; }
}
