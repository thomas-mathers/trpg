using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IChatDbContext : ITrpgDbContext
{
    DbSet<ChatMessage> ChatMessages { get; }
}
