using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

public class SkillService(TrpgDbContext context)
{
    public async Task AddSkill(Guid personId, Guid skillId, CancellationToken cancellationToken = default)
    {
        var prerequisites = await context.SkillPrerequisites
            .Where(sp => sp.SkillId == skillId)
            .Select(sp => sp.PrerequisiteSkillId)
            .ToListAsync(cancellationToken);

        if (prerequisites.Count > 0)
        {
            var knownSkillIds = await context.PersonSkills
                .Where(ps => ps.PersonId == personId)
                .Select(ps => ps.SkillId)
                .ToListAsync(cancellationToken);

            if (!prerequisites.All(knownSkillIds.Contains))
            {
                throw new InvalidOperationException("Skill prerequisites not met.");
            }
        }

        context.PersonSkills.Add(new PersonSkill
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            SkillId = skillId,
            Cooldown = 0
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Skill?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Skills.FindAsync([id], cancellationToken);

    public async Task<ReadOnlyCollection<Skill>> GetAll(CancellationToken cancellationToken = default)
    {
        var list = await context.Skills.ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<ReadOnlyCollection<PersonSkill>> GetAllByPersonId(Guid personId, CancellationToken cancellationToken = default)
    {
        var list = await context.PersonSkills
            .Include(ps => ps.Skill)
            .Where(ps => ps.PersonId == personId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<ReadOnlyCollection<Guid>> GetAllPrerequisites(Guid id, CancellationToken cancellationToken = default)
    {
        var list = await context.SkillPrerequisites
            .Where(sp => sp.SkillId == id)
            .Select(sp => sp.PrerequisiteSkillId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task RemoveSkill(Guid personId, Guid skillId, CancellationToken cancellationToken = default)
        => await context.PersonSkills
            .Where(ps => ps.PersonId == personId && ps.SkillId == skillId)
            .ExecuteDeleteAsync(cancellationToken);
}
