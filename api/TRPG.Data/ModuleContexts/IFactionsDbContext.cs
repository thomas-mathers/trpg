using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IFactionsDbContext : ITrpgDbContext
{
    DbSet<FactionMember> FactionMembers { get; }
    DbSet<Faction> Factions { get; }
}
