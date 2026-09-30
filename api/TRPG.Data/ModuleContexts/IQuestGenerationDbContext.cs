using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IQuestGenerationDbContext : ITrpgDbContext
{
    DbSet<QuestSeedSchedule> QuestSeedSchedules { get; }
    DbSet<QuestChainGenerationRequest> QuestChainGenerationRequests { get; }
}
