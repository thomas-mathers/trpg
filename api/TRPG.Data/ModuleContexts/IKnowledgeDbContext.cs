using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IKnowledgeDbContext : ITrpgDbContext
{
    DbSet<CreatureKnowledge> CreatureKnowledge { get; }
    DbSet<Relationship> Relationships { get; }
}
