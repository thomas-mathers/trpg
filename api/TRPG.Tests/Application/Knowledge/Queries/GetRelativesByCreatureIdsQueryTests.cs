using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Knowledge.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Knowledge.Queries;

public sealed class GetRelativesByCreatureIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetRelativesByCreatureIdsQueryHandler _handler = null!;
    private readonly Creature _firstSubject = Builders.MakeCreature(WorldId);
    private readonly Creature _secondSubject = Builders.MakeCreature(WorldId);
    private readonly Creature _mother = Builders.MakeCreature(WorldId, name: "Mother Creature");
    private readonly Creature _father = Builders.MakeCreature(WorldId, name: "Father Creature");
    private readonly Creature _unrelated = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetRelativesByCreatureIdsQueryHandler>();

        _context.Creatures.AddRange(_firstSubject, _secondSubject, _mother, _father, _unrelated);
        _context.Relationships.AddRange(
            new Relationship
            {
                WorldId = WorldId,
                SubjectId = _firstSubject.Id,
                RelativeId = _mother.Id,
                RelationshipType = RelationshipType.Mother,
            },
            new Relationship
            {
                WorldId = WorldId,
                SubjectId = _secondSubject.Id,
                RelativeId = _father.Id,
                RelationshipType = RelationshipType.Father,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_GroupsRelativesByEachRequestedSubject()
    {
        // Act
        var result = await _handler.Handle(
            new GetRelativesByCreatureIdsQuery
            {
                CreatureIds = [_firstSubject.Id, _secondSubject.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var firstSubjectRelative = Assert.Single(result[_firstSubject.Id]);
        Assert.Equal(_mother.Id, firstSubjectRelative.RelativeId);
        var secondSubjectRelative = Assert.Single(result[_secondSubject.Id]);
        Assert.Equal(_father.Id, secondSubjectRelative.RelativeId);
    }

    [Fact]
    public async Task Handle_OmitsASubject_WithNoRelatives()
    {
        // Act
        var result = await _handler.Handle(
            new GetRelativesByCreatureIdsQuery { CreatureIds = [_unrelated.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.ContainsKey(_unrelated.Id));
    }
}
