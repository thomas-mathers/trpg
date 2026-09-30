using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Data;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;
using TRPG.Quests.Jobs;

namespace TRPG.Tests.Quests.Jobs;

public sealed class QuestChainGenerationStartupRecoveryTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task StartAsync_FailsOnlyInterruptedRequests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var interrupted = new QuestChainGenerationRequest
        {
            WorldId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            Status = QuestChainGenerationStatus.InProgress,
        };
        var pending = new QuestChainGenerationRequest
        {
            WorldId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
        };
        await using (var context = database.CreateContext())
        {
            context.QuestChainGenerationRequests.AddRange(interrupted, pending);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using var services = BuildServices();
        var recovery = new QuestChainGenerationStartupRecovery(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QuestChainGenerationStartupRecovery>.Instance
        );

        await recovery.StartAsync(cancellationToken);

        await using var verification = database.CreateContext();
        var statuses = await verification
            .QuestChainGenerationRequests.Where(request =>
                request.Id == interrupted.Id || request.Id == pending.Id
            )
            .ToDictionaryAsync(request => request.Id, request => request.Status, cancellationToken);
        Assert.Equal(QuestChainGenerationStatus.Failed, statuses[interrupted.Id]);
        Assert.Equal(QuestChainGenerationStatus.Pending, statuses[pending.Id]);
    }

    private ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddDbContext<TrpgDbContext>(options => options.UseNpgsql(database.ConnectionString))
            .AddScoped<IQuestGenerationDbContext>(services =>
                services.GetRequiredService<TrpgDbContext>()
            )
            .BuildServiceProvider();
}
