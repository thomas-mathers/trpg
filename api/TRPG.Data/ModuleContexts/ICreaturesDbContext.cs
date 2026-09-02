using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface ICreaturesDbContext : ITrpgDbContext
{
    DbSet<Creature> Creatures { get; }
    DbSet<CreatureSkill> CreatureSkills { get; }
    DbSet<CreatureProfile> CreatureProfiles { get; }
}
