using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureKnowledgeQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreatureKnowledgeQueryHandler _handler = null!;
    private Guid _worldId;
    private Creature _asker = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureKnowledgeQueryHandler(_context);
        _worldId = Guid.NewGuid();
        _asker = Builders.MakeCreature(_worldId);
        _context.Creatures.Add(_asker);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    private GetCreatureKnowledgeQuery MakeQuery(string subjectName)
    {
        return new GetCreatureKnowledgeQuery
        {
            WorldId = _worldId,
            SubjectName = subjectName,
            CurrentYear = 975,
            AskingPerson = _asker,
        };
    }

    private async Task<Creature> SeedKnownPerson(string name)
    {
        var person = Builders.MakeCreature(_worldId, name: name);
        _context.Creatures.Add(person);
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                KnowerId = _asker.Id,
                SubjectId = person.Id,
                SubjectType = KnowledgeSubjectType.Creature,
                WorldId = _worldId,
            }
        );
        await _context.SaveChangesAsync();
        return person;
    }

    [Fact]
    public async Task Handle_ReturnsFullResultForBestMatch_WhenFirstNameOnlyIsGiven()
    {
        // Arrange
        await SeedKnownPerson("Elly Tealeaf");

        // Act
        var matches = await _handler.Handle(
            MakeQuery("Elly"),
            TestContext.Current.CancellationToken
        );

        // Assert
        var match = Assert.Single(matches);
        Assert.Equal("Elly Tealeaf", match.Name);
        Assert.Equal(1.0, match.Similarity, 3);
        var person = Assert.IsType<PersonLookupResult>(match.Result);
        Assert.Equal("Elly Tealeaf", person.Name);
    }

    [Fact]
    public async Task Handle_ResolvesMisspelling_WhenNameIsClose()
    {
        // Arrange
        await SeedKnownPerson("Elly Tealeaf");

        // Act
        var matches = await _handler.Handle(
            MakeQuery("Ellie"),
            TestContext.Current.CancellationToken
        );

        // Assert
        var match = Assert.Single(matches);
        Assert.Equal("Elly Tealeaf", match.Name);
        Assert.NotNull(match.Result);
    }

    [Fact]
    public async Task Handle_ReturnsStubsAfterBestMatch_WhenMultipleNamesMatch()
    {
        // Arrange
        await SeedKnownPerson("Elly Tealeaf");
        await SeedKnownPerson("Elly Brown");

        // Act
        var matches = await _handler.Handle(
            MakeQuery("Elly"),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.NotNull(matches[0].Result);
        Assert.Null(matches[1].Result);
        Assert.All(matches, m => Assert.Equal(1.0, m.Similarity, 3));
        Assert.Equal(
            new[] { "Elly Brown", "Elly Tealeaf" },
            matches.Select(m => m.Name).OrderBy(n => n).ToArray()
        );
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNothingResemblesTheName()
    {
        // Arrange
        await SeedKnownPerson("Elly Tealeaf");

        // Act
        var matches = await _handler.Handle(
            MakeQuery("Zzyzx"),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(matches);
    }

    [Fact]
    public async Task Handle_ExcludesMatchingEntity_WhenAskerHasNoKnowledgeOfIt()
    {
        // Arrange — the person exists but no knowledge row links the asker to them
        var stranger = Builders.MakeCreature(_worldId, name: "Elly Tealeaf");
        _context.Creatures.Add(stranger);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var matches = await _handler.Handle(
            MakeQuery("Elly Tealeaf"),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(matches);
    }

    [Fact]
    public async Task Handle_ReturnsCityResult_WhenAKnownCityMatches()
    {
        // Arrange
        var country = Builders.MakeCountry(_worldId);
        var state = Builders.MakeState(country.Id, _worldId);
        var city = Builders.MakeCity(state.Id, country.Id, worldId: _worldId, name: "Darkwick");
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Cities.Add(city);
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                KnowerId = _asker.Id,
                SubjectId = city.Id,
                SubjectType = KnowledgeSubjectType.City,
                WorldId = _worldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var matches = await _handler.Handle(
            MakeQuery("Darkwick"),
            TestContext.Current.CancellationToken
        );

        // Assert
        var match = Assert.Single(matches);
        Assert.Equal(nameof(KnowledgeSubjectType.City), match.SubjectType);
        var cityResult = Assert.IsType<CityLookupResult>(match.Result);
        Assert.Equal("Darkwick", cityResult.Name);
    }
}
