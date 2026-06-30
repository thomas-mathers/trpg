namespace TRPG.Models;

internal class PersonSkill {
    public int Experience { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Level { get; set; }
    public Guid PersonId { get; init; }
    public Skill Skill { get; init; }
}