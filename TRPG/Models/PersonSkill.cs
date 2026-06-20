namespace TRPG.Models;

public class PersonSkill
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public Guid SkillId { get; init; }
    public int Cooldown { get; set; }
    public Skill Skill { get; init; } = null!;
}
