namespace TRPG.Domain.Models;

public record Attributes
{
    public int CarryingCapacity { get; set; }
    public int Defense { get; set; }
    public int Dexterity { get; set; }
    public int Endurance { get; set; }
    public float FireResistance { get; set; }
    public float IceResistance { get; set; }
    public int Intelligence { get; set; }
    public float LightningResistance { get; set; }
    public float MagicResistance { get; set; }
    public int Mana { get; set; }
    public int MaximumAp { get; set; }
    public int MaximumHp { get; set; }
    public int MaximumMp { get; set; }
    public float MovementSpeed { get; set; }
    public float PhysicalResistance { get; set; }
    public float PoisonResistance { get; set; }
    public int Stamina { get; set; }
    public int Strength { get; set; }
}
