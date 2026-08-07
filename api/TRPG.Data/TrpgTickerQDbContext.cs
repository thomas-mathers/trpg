using Microsoft.EntityFrameworkCore;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.Utilities.Entities;

namespace TRPG.Data;

public class TrpgTimeTicker : TimeTickerEntity<TrpgTimeTicker>
{
    public string? ResultJson { get; set; }
}

public class TrpgCronTicker : CronTickerEntity;

public class TrpgTickerQDbContext(DbContextOptions<TrpgTickerQDbContext> options)
    : TickerQDbContext<TrpgTimeTicker, TrpgCronTicker>(options)
{
    public DbSet<TrpgTimeTicker> TimeTickers => Set<TrpgTimeTicker>();
}
