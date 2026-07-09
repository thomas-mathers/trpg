namespace TRPG.Models;

internal enum WorkstationType
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

internal class Workstation : Prop
{
    public Guid? AssignedCreatureId { get; set; }
    public Guid? OccupantId { get; set; }
    public WorkstationType WorkstationType { get; init; }
}
