using Microsoft.EntityFrameworkCore;
using Npgsql;
using TRPG.Data;

namespace TRPG.Application.Game;

internal static class GameSessionDbContextFactory
{
    public static TrpgDbContext Create(NpgsqlConnection connection)
    {
        var options = new DbContextOptionsBuilder<TrpgDbContext>().UseNpgsql(connection).Options;
        return new TrpgDbContext(options);
    }
}
