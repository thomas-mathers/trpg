using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IReputationsDbContext : ITrpgDbContext
{
    DbSet<Reputation> Reputations { get; }
    DbSet<ReputationLogEntry> ReputationLogEntries { get; }
}
