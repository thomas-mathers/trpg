namespace TRPG.Models;

internal class Attributes {
    public Meter Ap { get; set; } = null!;
    public int Defense { get; set; }
    public int Dexterity { get; set; }
    public int Endurance { get; set; }
    public float FireResistance { get; set; }
    public Meter Hp { get; set; } = null!;
    public float IceResistance { get; set; }
    public int Intelligence { get; set; }
    public float LightningResistance { get; set; }
    public float MagicResistance { get; set; }
    public float MovementSpeed { get; set; }
    public float PhysicalResistance { get; set; }
    public float PoisonResistance { get; set; }
    public int Strength { get; set; }
}
