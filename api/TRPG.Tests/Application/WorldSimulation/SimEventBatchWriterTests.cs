using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Persistence;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class SimEventBatchWriterTests(DatabaseFixture db) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Write_CommitsTheBatch_WhenAnotherStampCommitsAfterItsVersionRead()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = db.CreateContext();
        var world = Builders.MakeWorld();
        var creature = Builders.MakeCreature(world.Id);
        context.Worlds.Add(world);
        context.Creatures.Add(creature);
        await context.SaveChangesAsync(cancellationToken);

        await using var competing = db.CreateContext();
        await using var transaction = await competing.Database.BeginTransactionAsync(
            cancellationToken
        );
        await competing
            .Worlds.Where(entry => entry.Id == world.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        entry => entry.StateVersion,
                        entry => entry.StateVersion + 1
                    ),
                cancellationToken
            );
        var interceptor = new CompetingStampInterceptor(() =>
            transaction.CommitAsync(cancellationToken)
        );
        await using var services = BuildServices(context, interceptor);
        var writer = new SimEventBatchWriter(
            world.Id,
            services.GetRequiredService<IServiceScopeFactory>()
        );
        var stop = new Point(12, 34);

        await writer.Write(
            [new LocalMoveInterrupted(creature.Id, GameClock.Epoch, creature.LocationId, stop)],
            cancellationToken
        );

        await using var verify = db.CreateContext();
        var saved = await verify.Creatures.SingleAsync(
            entry => entry.Id == creature.Id,
            cancellationToken
        );
        Assert.Equal(
            (stop.X, stop.Y, CreatureMovement.Stationary),
            (saved.X, saved.Y, saved.Movement)
        );
        Assert.Equal(2, await writer.ReadMarker(cancellationToken));
        Assert.Equal(2, interceptor.StampAttempts);
    }

    private ServiceProvider BuildServices(
        TrpgDbContext context,
        CompetingStampInterceptor interceptor
    ) =>
        new ServiceCollection()
            .AddTrpgTestServices(context)
            .RemoveAll<TrpgDbContext>()
            .AddDbContext<TrpgDbContext>(options =>
                options.UseNpgsql(db.ConnectionString).AddInterceptors(interceptor)
            )
            .BuildServiceProvider();

    private sealed class CompetingStampInterceptor(Func<Task> commitCompetingStamp)
        : DbCommandInterceptor
    {
        public int StampAttempts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                command.CommandText.Contains("UPDATE worlds", StringComparison.Ordinal)
                && command.CommandText.Contains("state_version", StringComparison.Ordinal)
            )
            {
                StampAttempts++;
                if (StampAttempts == 1)
                    await commitCompetingStamp();
            }

            return result;
        }
    }
}
