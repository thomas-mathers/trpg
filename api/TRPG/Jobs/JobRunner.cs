using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Serialization;
using TRPG.Jobs.Responses;

namespace TRPG.Jobs;

internal sealed record JobSnapshot(
    Guid JobId,
    JobStatus Status,
    string? ResultJson,
    string? ErrorMessage,
    DateTimeOffset? FinishedAt
);

internal sealed class JobRunner(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<JobRunner> logger
) : BackgroundService
{
    public const int WorkerCount = 2;
    public static readonly TimeSpan FinishedJobRetention = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<Guid, JobSnapshot> _jobs = new();
    private readonly Channel<QueuedJob> _queue = Channel.CreateUnbounded<QueuedJob>();

    public Guid Schedule<TResult>(Func<IServiceProvider, CancellationToken, Task<TResult>> work) =>
        Enqueue(
            async (services, cancellationToken) =>
            {
                var result = await work(services, cancellationToken);
                return JsonSerializer.Serialize(result, TrpgJsonOptions.Default);
            }
        );

    public Guid Schedule(Func<IServiceProvider, CancellationToken, Task> work) =>
        Enqueue(
            async (services, cancellationToken) =>
            {
                await work(services, cancellationToken);
                return (string?)null;
            }
        );

    public JobSnapshot? Get(Guid jobId) => _jobs.GetValueOrDefault(jobId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable
            .Range(0, WorkerCount)
            .Select(_ => Task.Run(() => RunWorker(stoppingToken), CancellationToken.None));
        await Task.WhenAll(workers);
    }

    private Guid Enqueue(Func<IServiceProvider, CancellationToken, Task<string?>> work)
    {
        RemoveExpiredJobs();

        var job = new QueuedJob(Guid.NewGuid(), work);
        _jobs[job.JobId] = new JobSnapshot(job.JobId, JobStatus.Queued, null, null, null);

        var transaction = Transaction.Current;
        if (transaction is null)
        {
            _queue.Writer.TryWrite(job);
        }
        else
        {
            // The job reads rows the caller's transaction wrote, so it may only start once they are committed.
            transaction.TransactionCompleted += (_, completed) =>
                OnTransactionCompleted(job, completed.Transaction.TransactionInformation.Status);
        }

        return job.JobId;
    }

    private void OnTransactionCompleted(QueuedJob job, TransactionStatus status)
    {
        if (status == TransactionStatus.Committed)
        {
            _queue.Writer.TryWrite(job);
            return;
        }

        Finish(job.JobId, JobStatus.Failed, null, "The scheduling transaction did not commit.");
    }

    private async Task RunWorker(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await Execute(job, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task Execute(QueuedJob job, CancellationToken stoppingToken)
    {
        _jobs[job.JobId] = _jobs[job.JobId] with { Status = JobStatus.InProgress };

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var resultJson = await job.Work(scope.ServiceProvider, stoppingToken);
            Finish(job.JobId, JobStatus.Done, resultJson, null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Job {JobId} failed.", job.JobId);
            Finish(job.JobId, JobStatus.Failed, null, exception.Message);
        }
    }

    private void Finish(Guid jobId, JobStatus status, string? resultJson, string? errorMessage) =>
        _jobs[jobId] = new JobSnapshot(
            jobId,
            status,
            resultJson,
            errorMessage,
            timeProvider.GetUtcNow()
        );

    private void RemoveExpiredJobs()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var (jobId, snapshot) in _jobs)
        {
            if (now - snapshot.FinishedAt >= FinishedJobRetention)
            {
                _jobs.TryRemove(jobId, out _);
            }
        }
    }

    private sealed record QueuedJob(
        Guid JobId,
        Func<IServiceProvider, CancellationToken, Task<string?>> Work
    );
}
