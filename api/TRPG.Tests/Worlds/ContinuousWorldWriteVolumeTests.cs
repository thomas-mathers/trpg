using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Common.Events;
using TRPG.Application.Worlds;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;
using TRPG.Worlds;

namespace TRPG.Tests.Worlds;

public sealed class ContinuousWorldWriteVolumeTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly string[] WriteVerbs = ["INSERT", "UPDATE", "DELETE"];

    private readonly ManualTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );
    private readonly RecordingCommandInterceptor _interceptor = new();
    private readonly World _world = Builders.MakeWorld();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WorldClock _worldClock = null!;
    private ContinuousWorldProcessor _processor = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .RemoveAll<TrpgDbContext>()
            .AddDbContext<TrpgDbContext>(options =>
                options.UseNpgsql(db.ConnectionString).AddInterceptors(_interceptor)
            )
            .AddScoped<IGameClientEventDispatcher, DiscardingGameClientEventDispatcher>()
            .BuildServiceProvider();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _worldClock = new WorldClock(scopeFactory, _timeProvider);
        _processor = new ContinuousWorldProcessor(
            scopeFactory,
            _worldClock,
            new WorldMutationGate(),
            NullLogger<ContinuousWorldProcessor>.Instance
        );

        var country = Builders.MakeCountry(_world.Id);
        var state = Builders.MakeState(country.Id);
        var location = Builders.MakeLocation(_world.Id, state.Id, kind: LocationKind.Wilderness);
        var player = Builders.MakeCreature(_world.Id, locationId: location.Id);
        _world.PlayerId = player.Id;
        _context.Worlds.Add(_world);
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task ProcessFrequent_WritesOnlyTheClockCheckpoint_WhenNothingChanges()
    {
        // Arrange
        await _processor.ProcessFrequent(TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        _interceptor.Clear();

        // Act
        await _processor.ProcessFrequent(TestContext.Current.CancellationToken);

        // Assert
        var write = Assert.Single(_interceptor.Writes);
        Assert.Contains("UPDATE worlds", write);
    }

    [Fact]
    public async Task ProcessRoutines_WritesNothing_WhenNothingIsDue()
    {
        // Arrange
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        _interceptor.Clear();

        // Act
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_interceptor.Writes);
    }

    private sealed class RecordingCommandInterceptor : DbCommandInterceptor
    {
        private readonly List<string> _statements = [];

        public IReadOnlyList<string> Writes
        {
            get
            {
                lock (_statements)
                {
                    return _statements
                        .Where(statement =>
                            WriteVerbs.Any(verb =>
                                statement.TrimStart().StartsWith(verb, StringComparison.Ordinal)
                            )
                        )
                        .ToArray();
                }
            }
        }

        public void Clear()
        {
            lock (_statements)
            {
                _statements.Clear();
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            Record(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            Record(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Record(DbCommand command)
        {
            lock (_statements)
            {
                _statements.Add(command.CommandText);
            }
        }
    }

    private sealed class DiscardingGameClientEventDispatcher(TestGameClientEventSink sink)
        : IGameClientEventDispatcher
    {
        public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default)
        {
            sink.EnqueuedEvents.Clear();
            return Task.FromResult(false);
        }
    }
}
