namespace TRPG.Models;

internal class SkillPrerequisite
{
    public Guid SkillId { get; init; }
    public Guid PrerequisiteSkillId { get; init; }
}
