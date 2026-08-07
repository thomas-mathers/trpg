using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TRPG.Data;

internal sealed class TickerQDbContextFactory : IDesignTimeDbContextFactory<TrpgTickerQDbContext>
{
    public TrpgTickerQDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrpgTickerQDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=5432;Database=trpg;Username=postgres;Password=postgres",
                sql =>
                {
                    sql.MigrationsAssembly("TRPG.Data");
                    sql.MigrationsHistoryTable("__TickerQMigrationsHistory");
                }
            )
            .Options;

        return new TrpgTickerQDbContext(options);
    }
}
