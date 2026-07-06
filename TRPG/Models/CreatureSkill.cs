namespace TRPG.Models;

internal class CreatureSkill {
    public Guid CreatureId { get; init; }
    public int Experience { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Level { get; set; }
    public Skill Skill { get; init; }
    public Guid WorldId { get; init; }
}
