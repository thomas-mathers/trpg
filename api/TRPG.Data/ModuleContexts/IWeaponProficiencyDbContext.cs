using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IWeaponProficiencyDbContext : ITrpgDbContext
{
    DbSet<CreatureWeaponProficiency> CreatureWeaponProficiencies { get; }
}
