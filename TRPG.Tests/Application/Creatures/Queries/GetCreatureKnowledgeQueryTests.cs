using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureKnowledgeQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly string[] EllyMatches = ["Elly Brown", "Elly Tealeaf"];
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetCreatureKnowledgeQueryHandler _handler = null!;
    private readonly Creature _asker = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureKnowledgeQueryHandler(_context);
        await _context.AddCreature(_asker, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    private GetCreatureKnowledgeQuery MakeQuery(string subjectName)
    {
        return new GetCreatureKnowledgeQuery
        {
            WorldId = WorldId,
            SubjectName = subjectName,
            CurrentYear = 975,
            AskingPerson = _asker,
        };
    }

    private async Task<Creature> SeedKnownPerson(string name)
    {
        var person = Builders.MakeCreature(WorldId, name: name);
        _context.Creatures.Add(person);
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                KnowerId = _asker.Id,
                SubjectId = person.Id,
                SubjectType = KnowledgeSubjectType.Creature,
                WorldId = WorldId,
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
        Assert.Equal(EllyMatches, matches.Select(m => m.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task Handle_ExcludesTheAskingPerson_WhenTheirOwnNameMatches()
    {
        // Arrange — the asker shares a surname with the subject, so their own name also matches
        var asker = Builders.MakeCreature(WorldId, name: "Thokk Warscar");
        _context.Creatures.Add(asker);
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                KnowerId = asker.Id,
                SubjectId = asker.Id,
                SubjectType = KnowledgeSubjectType.Creature,
                WorldId = WorldId,
            }
        );
        var wife = Builders.MakeCreature(WorldId, name: "Ruzka Warscar");
        _context.Creatures.Add(wife);
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                KnowerId = asker.Id,
                SubjectId = wife.Id,
                SubjectType = KnowledgeSubjectType.Creature,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var matches = await _handler.Handle(
            new GetCreatureKnowledgeQuery
            {
                WorldId = WorldId,
                SubjectName = "Ruzka Warscar",
                CurrentYear = 975,
                AskingPerson = asker,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var match = Assert.Single(matches);
        Assert.Equal("Ruzka Warscar", match.Name);
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
        var stranger = await _context.AddCreature(
            Builders.MakeCreature(WorldId, name: "Elly Tealeaf"),
            TestContext.Current.CancellationToken
        );

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
        var country = Builders.MakeCountry(WorldId);
        var state = Builders.MakeState(country.Id, WorldId);
        var city = Builders.MakeCity(state.Id, country.Id, worldId: WorldId, name: "Darkwick");
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Cities.Add(city);
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                KnowerId = _asker.Id,
                SubjectId = city.Id,
                SubjectType = KnowledgeSubjectType.City,
                WorldId = WorldId,
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
