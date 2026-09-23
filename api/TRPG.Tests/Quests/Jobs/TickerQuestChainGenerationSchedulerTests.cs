using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickerQ.Utilities;
using TickerQ.Utilities.Enums;
using TickerQ.Utilities.Interfaces;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Quests.Jobs;

namespace TRPG.Tests.Quests.Jobs;

[Collection("Endpoints")]
public sealed class TickerQuestChainGenerationSchedulerTests(EndpointTestFixture fixture)
{
    [Fact]
    public async Task ScheduleAsync_DefersDispatch_WhenCalledInsideAmbientTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestId = Guid.NewGuid();
        var command = new GenerateQuestChainCommand
        {
            RequestId = requestId,
            ChainPremise = "A scheduler transaction regression test.",
            MinimumChainLength = 2,
            MaximumChainLength = 2,
            AvailableEntities = [],
        };
        var scheduledAfter = DateTime.UtcNow;

        await using (var scope = fixture.CreateScope())
        {
            var scheduler =
                scope.ServiceProvider.GetRequiredService<IQuestChainGenerationScheduler>();
            using var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                TransactionScopeAsyncFlowOption.Enabled
            );

            await scheduler.ScheduleAsync(command, cancellationToken);

            transaction.Complete();
        }

        await using var context = CreateTickerContext();
        var functionName = TickerFunctionProvider.GetFunctionName<GenerateQuestChainJob>();
        var candidates = await context
            .TimeTickers.AsNoTracking()
            .Where(ticker => ticker.Function == functionName && ticker.CreatedAt >= scheduledAfter)
            .ToArrayAsync(cancellationToken);
        var ticker = Assert.Single(
            candidates,
            candidate =>
                TickerHelper
                    .ReadTickerRequest<GenerateQuestChainCommand>(candidate.Request)
                    .RequestId == requestId
        );

        Assert.True(
            ticker.ExecutionTime
                >= scheduledAfter.Add(TickerQuestChainGenerationScheduler.DispatchDelay)
        );
        Assert.Equal(TickerStatus.Idle, ticker.Status);

        await context
            .TimeTickers.Where(candidate => candidate.Id == ticker.Id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private TrpgTickerQDbContext CreateTickerContext() =>
        new(
            new DbContextOptionsBuilder<TrpgTickerQDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .Options
        );
}
