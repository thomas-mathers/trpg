using Microsoft.EntityFrameworkCore;
using TRPG.Commands;
using TRPG.Data;
using TRPG.EntityDefinitions;
using TRPG.Models;

namespace TRPG.Services;

internal class AbilityService(TrpgDbContext context, AbilityDefinitions abilityDefinitions) {
    public async Task AddAbility(Guid personId, string abilityName, CancellationToken cancellationToken = default) {
        var ability = abilityDefinitions.GetAbility(abilityName) ??
                      throw new InvalidOperationException($"Unknown ability: {abilityName}");

        var skillLevel = await context.PersonSkills
            .Where(ps => ps.PersonId == personId && ps.Skill == ability.Skill)
            .Select(ps => (int?) ps.Level)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        if (skillLevel < ability.RequiredSkillLevel) {
            throw new InvalidOperationException(
                $"Requires {ability.Skill} level {ability.RequiredSkillLevel} (current: {skillLevel}).");
        }

        var prerequisites = abilityDefinitions.GetPrerequisites(abilityName);
        if (prerequisites.Count > 0) {
            var knownAbilityNames = await context.PersonAbilities
                .Where(pa => pa.PersonId == personId)
                .Select(pa => pa.AbilityName)
                .ToListAsync(cancellationToken);

            if (!prerequisites.All(knownAbilityNames.Contains)) {
                throw new InvalidOperationException("Ability prerequisites not met.");
            }
        }

        context.PersonAbilities.Add(new PersonAbility { PersonId = personId, AbilityName = abilityName });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PersonAbility>> GetAllByPersonId(Guid personId,
        CancellationToken cancellationToken = default) {
        var list = await context.PersonAbilities
            .Where(pa => pa.PersonId == personId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task RemoveAbility(Guid personId, string abilityName, CancellationToken cancellationToken = default) {
        await context.PersonAbilities
            .Where(pa => pa.PersonId == personId && pa.AbilityName == abilityName)
            .ExecuteDeleteAsync(cancellationToken);
    }
}