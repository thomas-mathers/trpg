using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TRPG.Data.ModuleContexts;

public interface ITrpgDbContext
{
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;
}
