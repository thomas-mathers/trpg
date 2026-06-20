using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TRPG.Data;

public class TrpgDbContextFactory : IDesignTimeDbContextFactory<TrpgDbContext>
{
    public TrpgDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrpgDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=trpg;Username=postgres;Password=postgres")
            .Options;

        return new TrpgDbContext(options);
    }
}
