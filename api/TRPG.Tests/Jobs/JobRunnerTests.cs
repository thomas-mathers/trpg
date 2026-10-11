using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Common.Serialization;
using TRPG.Jobs;
using TRPG.Jobs.Responses;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Jobs;

public sealed class JobRunnerTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();
    private readonly ManualTimeProvider _timeProvider = new(DateTimeOffset.UnixEpoch);
    private JobRunner _runner = null!;

    public async ValueTask InitializeAsync()
    {
        _runner = new JobRunner(
            _services.GetRequiredService<IServiceScopeFactory>(),
            _timeProvider,
            NullLogger<JobRunner>.Instance
        );
        await _runner.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _runner.StopAsync(CancellationToken.None);
        _runner.Dispose();
        await _services.DisposeAsync();
    }

    [Fact]
    public async Task Schedule_StoresSerializedResult_WhenJobSucceeds()
    {
        // Arrange
        var expected = new SampleResult("done", 3);

        // Act
        var jobId = _runner.Schedule((_, _) => Task.FromResult(expected));

        // Assert
        var snapshot = await WaitForFinish(jobId);
        Assert.Equal(JobStatus.Done, snapshot.Status);
        Assert.Equal(
            expected,
            JsonSerializer.Deserialize<SampleResult>(snapshot.ResultJson!, TrpgJsonOptions.Default)
        );
    }

    [Fact]
    public async Task Schedule_MarksJobDone_WhenJobHasNoResult()
    {
        // Act
        var jobId = _runner.Schedule((_, _) => Task.CompletedTask);

        // Assert
        var snapshot = await WaitForFinish(jobId);
        Assert.Equal(JobStatus.Done, snapshot.Status);
        Assert.Null(snapshot.ResultJson);
    }

    [Fact]
    public async Task Schedule_MarksJobFailedWithMessage_WhenJobThrows()
    {
        // Act
        var jobId = _runner.Schedule<SampleResult>(
            (_, _) => throw new InvalidOperationException("boom")
        );

        // Assert
        var snapshot = await WaitForFinish(jobId);
        Assert.Equal(JobStatus.Failed, snapshot.Status);
        Assert.Equal("boom", snapshot.ErrorMessage);
    }

    [Fact]
    public async Task Schedule_ResolvesServicesFromAFreshScope_WhenJobRuns()
    {
        // Arrange
        var scopedServices = new List<IServiceProvider>();

        // Act
        var firstJobId = _runner.Schedule(
            (services, _) =>
            {
                scopedServices.Add(services);
                return Task.CompletedTask;
            }
        );
        await WaitForFinish(firstJobId);
        var secondJobId = _runner.Schedule(
            (services, _) =>
            {
                scopedServices.Add(services);
                return Task.CompletedTask;
            }
        );

        // Assert
        await WaitForFinish(secondJobId);
        Assert.Equal(2, scopedServices.Distinct().Count());
    }

    [Fact]
    public async Task Schedule_LeavesExtraJobsQueued_WhenAllWorkersAreBusy()
    {
        // Arrange
        var release = new TaskCompletionSource();
        var started = new List<TaskCompletionSource>();
        for (var index = 0; index < JobRunner.WorkerCount; index++)
        {
            var signal = new TaskCompletionSource();
            started.Add(signal);
            _runner.Schedule(
                async (_, _) =>
                {
                    signal.SetResult();
                    await release.Task;
                }
            );
        }

        await Task.WhenAll(started.Select(signal => signal.Task)).WaitAsync(Timeout);

        // Act
        var queuedJobId = _runner.Schedule((_, _) => Task.CompletedTask);

        // Assert
        Assert.Equal(JobStatus.Queued, _runner.Get(queuedJobId)!.Status);
        release.SetResult();
        Assert.Equal(JobStatus.Done, (await WaitForFinish(queuedJobId)).Status);
    }

    [Fact]
    public async Task Schedule_DefersTheJob_UntilTheAmbientTransactionCommits()
    {
        // Arrange
        var ran = new TaskCompletionSource();
        Guid jobId;
        using (
            var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                TransactionScopeAsyncFlowOption.Enabled
            )
        )
        {
            // Act
            jobId = _runner.Schedule(
                (_, _) =>
                {
                    ran.SetResult();
                    return Task.CompletedTask;
                }
            );

            // Assert
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(ran.Task.IsCompleted);
            Assert.Equal(JobStatus.Queued, _runner.Get(jobId)!.Status);
            transaction.Complete();
        }

        await ran.Task.WaitAsync(Timeout);
        Assert.Equal(JobStatus.Done, (await WaitForFinish(jobId)).Status);
    }

    [Fact]
    public async Task Schedule_FailsTheJobWithoutRunningIt_WhenTheAmbientTransactionRollsBack()
    {
        // Arrange
        var ran = false;
        Guid jobId;
        using (
            new TransactionScope(
                TransactionScopeOption.Required,
                TransactionScopeAsyncFlowOption.Enabled
            )
        )
        {
            // Act
            jobId = _runner.Schedule(
                (_, _) =>
                {
                    ran = true;
                    return Task.CompletedTask;
                }
            );
        }

        // Assert
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(ran);
        Assert.Equal(JobStatus.Failed, _runner.Get(jobId)!.Status);
    }

    [Fact]
    public void Get_ReturnsNull_WhenJobIsUnknown()
    {
        // Act
        var snapshot = _runner.Get(Guid.NewGuid());

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenFinishedJobOutlivesRetention()
    {
        // Arrange
        var jobId = _runner.Schedule((_, _) => Task.CompletedTask);
        await WaitForFinish(jobId);
        _timeProvider.Advance(JobRunner.FinishedJobRetention + TimeSpan.FromSeconds(1));
        _runner.Schedule((_, _) => Task.CompletedTask);

        // Act
        var snapshot = _runner.Get(jobId);

        // Assert
        Assert.Null(snapshot);
    }

    private async Task<JobSnapshot> WaitForFinish(Guid jobId)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            var snapshot = _runner.Get(jobId);
            if (snapshot?.Status is JobStatus.Done or JobStatus.Failed)
            {
                return snapshot;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Job {jobId} did not finish.");
    }

    private sealed record SampleResult(string Label, int Count);
}
