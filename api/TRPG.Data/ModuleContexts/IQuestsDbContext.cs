using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IQuestsDbContext : ITrpgDbContext
{
    DbSet<CreatureQuestObjective> CreatureQuestObjectives { get; }
    DbSet<CreatureQuest> CreatureQuests { get; }
    DbSet<QuestObjective> QuestObjectives { get; }
    DbSet<Quest> Quests { get; }
    DbSet<FactDisclosureAttempt> FactDisclosureAttempts { get; }
    DbSet<FactDisclosureLockout> FactDisclosureLockouts { get; }
}
