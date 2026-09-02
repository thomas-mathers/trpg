using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface ICreatureJobsDbContext : ITrpgDbContext
{
    DbSet<CreatureJob> CreatureJobs { get; }
}
