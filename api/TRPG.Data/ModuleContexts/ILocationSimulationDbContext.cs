using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface ILocationSimulationDbContext : ITrpgDbContext
{
    DbSet<CreatureSpawner> CreatureSpawners { get; }
    DbSet<RestockPolicy> RestockPolicies { get; }
}
