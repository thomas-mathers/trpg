namespace TRPG.Domain.Models;

public class CreatureWeaponProficiency
{
    public Guid WorldId { get; init; }
    public Guid CreatureId { get; init; }
    public WeaponType WeaponType { get; init; }
    public int Proficiency { get; set; }
}
