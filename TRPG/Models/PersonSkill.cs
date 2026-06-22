namespace TRPG.Models;

internal class PersonSkill {
    public int Cooldown { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public Skill Skill { get; init; } = null!;
    public Guid SkillId { get; init; }
}