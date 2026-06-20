namespace TRPG.Models;

public class SkillPrerequisite
{
    public Guid SkillId { get; init; }
    public Guid PrerequisiteSkillId { get; init; }
}
