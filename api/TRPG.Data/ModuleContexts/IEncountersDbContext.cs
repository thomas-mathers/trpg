using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IEncountersDbContext : ITrpgDbContext
{
    DbSet<EncounterGroup> EncounterGroups { get; }
    DbSet<EncounterGroupMember> EncounterGroupMembers { get; }
    DbSet<Encounter> Encounters { get; }
}
