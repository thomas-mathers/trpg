using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Knowledge.Commands;
using TRPG.Application.Knowledge.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Knowledge.Commands;

public sealed class RecordRoomVisitCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _roomLocationId = Guid.NewGuid();
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<RecordRoomVisitCommand> _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ICommandHandler<RecordRoomVisitCommand>>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RemembersSomewhereTheCreatureHasStood()
    {
        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.CreatureKnowledge.AnyAsync(
                knowledge =>
                    knowledge.KnowerId == _player.Id
                    && knowledge.SubjectId == _roomLocationId
                    && knowledge.SubjectType == KnowledgeSubjectType.Room,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_RecordsAPlaceOnce_HoweverOftenItIsWalkedThrough()
    {
        // Arrange
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Single(
            await verifyContext
                .CreatureKnowledge.Where(knowledge =>
                    knowledge.KnowerId == _player.Id && knowledge.SubjectId == _roomLocationId
                )
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_LeavesSomewhereUnvisitedUnknown()
    {
        // Arrange
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);
        var elsewhere = Guid.NewGuid();

        // Act
        var visited = await _serviceProvider
            .GetRequiredService<IQueryHandler<GetVisitedRoomLocationIdsQuery, IReadOnlySet<Guid>>>()
            .Handle(
                new GetVisitedRoomLocationIdsQuery
                {
                    CreatureId = _player.Id,
                    RoomLocationIds = [_roomLocationId, elsewhere],
                },
                TestContext.Current.CancellationToken
            );

        // Assert — the query is what tells an exit apart from one leading back.
        Assert.Contains(_roomLocationId, visited);
        Assert.DoesNotContain(elsewhere, visited);
    }

    private RecordRoomVisitCommand MakeCommand() =>
        new()
        {
            WorldId = WorldId,
            CreatureId = _player.Id,
            RoomLocationId = _roomLocationId,
        };
}
