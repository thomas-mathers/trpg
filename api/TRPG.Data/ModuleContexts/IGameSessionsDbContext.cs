using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IGameSessionsDbContext : ITrpgDbContext
{
    DbSet<GameSession> GameSessions { get; }
}
