namespace TRPG.Data.Models;

public enum WorkstationType
{
    Alchemy,
    Armorsmithing,
    Carpentry,
    Cooking,
    Enchanting,
    Jewelcrafting,
    Tailoring,
    Trade,
    Prayer,
    Reading,
    Weaponsmithing,
}

public class Workstation : Prop
{
    public Guid? AssignedCreatureId { get; set; }
    public Guid? OccupantId { get; set; }
    public WorkstationType WorkstationType { get; init; }
}
