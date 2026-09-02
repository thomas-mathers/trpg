using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface INpcConversationsDbContext : ITrpgDbContext
{
    DbSet<NpcConversation> NpcConversations { get; }
    DbSet<NpcConversationHistory> NpcConversationHistories { get; }
    DbSet<NpcConversationSessionState> NpcConversationSessionStates { get; }
}
