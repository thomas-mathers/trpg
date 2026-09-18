using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Quests.Jobs;

internal sealed class QuestChainGenerationStartupRecovery(
    IServiceScopeFactory scopeFactory,
    ILogger<QuestChainGenerationStartupRecovery> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            await context
                .QuestChainGenerationRequests.Where(request =>
                    request.Status == QuestChainGenerationStatus.InProgress
                )
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            request => request.Status,
                            QuestChainGenerationStatus.Failed
                        ),
                    cancellationToken
                );
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to mark interrupted quest-chain generations as failed."
            );
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
