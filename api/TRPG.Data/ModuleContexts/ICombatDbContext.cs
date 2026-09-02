using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface ICombatDbContext : ITrpgDbContext
{
    DbSet<Encounter> Encounters { get; }
}
