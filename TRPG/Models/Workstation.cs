namespace TRPG.Models;

internal enum WorkstationType {
    Alchemy,
    Armorsmithing,
    Cooking,
    Enchanting,
    Trade,
    Prayer,
    Reading,
    Weaponsmithing
}

internal class Workstation : Prop {
    public Guid? AssignedPersonId { get; set; }
    public Guid? OccupantId { get; set; }
    public WorkstationType WorkstationType { get; init; }
}