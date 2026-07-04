using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class SkillService(TrpgDbContext context) {

    public async Task<PersonSkill> AddExperience(Guid personId, Skill skill, int amount,
        CancellationToken cancellationToken = default) {
        var personSkill = await context.PersonSkills
            .FirstOrDefaultAsync(ps => ps.PersonId == personId && ps.Skill == skill, cancellationToken);

        if (personSkill == null) {
            var worldId = await context.Persons
                .Where(p => p.Id == personId)
                .Select(p => p.WorldId)
                .FirstAsync(cancellationToken);
            personSkill = new PersonSkill { PersonId = personId, Skill = skill, WorldId = worldId };
            context.PersonSkills.Add(personSkill);
        }

        personSkill.Experience += amount;
        personSkill.Level = LevelForExperience(personSkill.Experience);

        await context.SaveChangesAsync(cancellationToken);
        return personSkill;
    }

    public async Task<IReadOnlyCollection<PersonSkill>> GetAllByPersonId(Guid personId,
        CancellationToken cancellationToken = default) {
        var list = await context.PersonSkills
            .Where(ps => ps.PersonId == personId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    private static int LevelForExperience(int xp) {
        for (var level = GameRules.MaxSkillLevel; level >= 1; level--) {
            if (xp >= GameRules.XpForSkillLevel(level)) return level;
        }
        return 0;
    }
}