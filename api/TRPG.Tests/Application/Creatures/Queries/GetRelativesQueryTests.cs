using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetRelativesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetRelativesQueryHandler _handler = null!;
    private readonly Creature _subject = Builders.MakeCreature(WorldId);
    private readonly Creature _mother = Builders.MakeCreature(WorldId, name: "Mother Creature");
    private readonly Creature _unrelated = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetRelativesQueryHandler>();

        _context.Creatures.AddRange(_subject, _mother, _unrelated);
        _context.Relationships.Add(
            new Relationship
            {
                WorldId = WorldId,
                SubjectId = _subject.Id,
                RelativeId = _mother.Id,
                RelationshipType = RelationshipType.Mother,
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
    public async Task Handle_ReturnsOnlyRelativesOfTheSubject()
    {
        // Act
        var result = await _handler.Handle(
            new GetRelativesQuery { CreatureId = _subject.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var relative = Assert.Single(result);
        Assert.Equal(_mother.Id, relative.RelativeId);
        Assert.Equal(_mother.Name, relative.Name);
        Assert.Equal(RelationshipType.Mother, relative.RelationshipType);
    }
}
