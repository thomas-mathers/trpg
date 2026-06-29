using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class SkillService(TrpgDbContext context) {
    private static readonly int[] LevelThresholds = [0, 100, 250, 450, 700, 1000, 1350, 1750, 2200, 2700];

    public async Task<PersonSkill> AddExperience(Guid personId, Skill skill, int amount,
        CancellationToken cancellationToken = default) {
        var personSkill = await context.PersonSkills
            .FirstOrDefaultAsync(ps => ps.PersonId == personId && ps.Skill == skill, cancellationToken);

        if (personSkill == null) {
            personSkill = new PersonSkill { PersonId = personId, Skill = skill };
            context.PersonSkills.Add(personSkill);
        }

        personSkill.Experience += amount;
        personSkill.Level = LevelForExperience(personSkill.Experience);

        await context.SaveChangesAsync(cancellationToken);
        return personSkill;
    }

    public async Task<ReadOnlyCollection<PersonSkill>> GetAllByPersonId(Guid personId,
        CancellationToken cancellationToken = default) {
        var list = await context.PersonSkills
            .Where(ps => ps.PersonId == personId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    private static int LevelForExperience(int xp) {
        for (var i = LevelThresholds.Length - 1; i >= 0; i--) {
            if (xp >= LevelThresholds[i]) return i;
        }
        return 0;
    }
}
