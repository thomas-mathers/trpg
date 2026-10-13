using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Commands;

public sealed class AddCellCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly RecordingPropAddedPublisher _publisher = new();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IDomainEventPublisher<PropAddedEvent>>(_publisher)
            .BuildServiceProvider();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PublishesPropAdded_ForTheCellsLocation()
    {
        // Arrange
        var cell = MakeCell();

        // Act
        await AddCell(cell);

        // Assert
        Assert.Equal(
            new PropAddedEvent(cell.WorldId, cell.LocationId),
            _publisher.Published.Single()
        );
    }

    [Fact]
    public async Task Handle_MakesTheNewCellVisibleToLocationPropQueries_AfterTheyWereCached()
    {
        // Arrange
        var cell = MakeCell();
        await GetProps(cell.LocationId);

        // Act
        await AddCell(cell);

        // Assert
        var props = await GetProps(cell.LocationId);
        Assert.Contains(props, p => p.Id == cell.Id);
    }

    private static Cell MakeCell() =>
        new()
        {
            WorldId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            X = 1,
            Y = 1,
            Width = 1,
            Depth = 1,
        };

    private Task AddCell(Cell cell) =>
        _serviceProvider
            .GetRequiredService<ICommandHandler<AddCellCommand>>()
            .Handle(new AddCellCommand { Cell = cell }, TestContext.Current.CancellationToken);

    private Task<IReadOnlyCollection<Prop>> GetProps(Guid locationId) =>
        _serviceProvider
            .GetRequiredService<
                IQueryHandler<GetPropsByLocationIdQuery, IReadOnlyCollection<Prop>>
            >()
            .Handle(
                new GetPropsByLocationIdQuery { LocationId = locationId },
                TestContext.Current.CancellationToken
            );

    private sealed class RecordingPropAddedPublisher : IDomainEventPublisher<PropAddedEvent>
    {
        public List<PropAddedEvent> Published { get; } = [];

        public Task Publish(
            PropAddedEvent domainEvent,
            CancellationToken cancellationToken = default
        )
        {
            Published.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
