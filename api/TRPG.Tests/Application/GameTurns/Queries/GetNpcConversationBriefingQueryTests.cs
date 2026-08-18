using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.Worlds;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Queries;

[Collection("Database")]
public sealed class GetNpcConversationBriefingQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetNpcConversationBriefingQueryHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, name: "Player");
    private readonly Creature _npc = Builders.MakeCreature(WorldId, name: "Npc");

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetNpcConversationBriefingQueryHandler>();
        _context.Creatures.AddRange(_player, _npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private GetNpcConversationBriefingQuery MakeQuery() =>
        new()
        {
            NpcId = _npc.Id,
            PlayerId = _player.Id,
            WorldId = WorldId,
        };

    [Fact]
    public async Task Handle_ReturnsIdentityFromTheCreature()
    {
        // Act
        var result = await _handler.Handle(
            MakeQuery(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(_npc.Name, result.Identity.Name);
        Assert.Equal(_npc.CreatureType.ToString(), result.Identity.Race);
        Assert.Equal(_npc.Gender, result.Identity.Gender);
        Assert.Equal(WorldEpoch.Year - _npc.BirthYear, result.Identity.Age);
    }

    [Fact]
    public async Task Handle_ReturnsProfileData_WhenAProfileExists()
    {
        // Arrange
        var factionId = Guid.NewGuid();
        _context.NpcProfiles.Add(
            new NpcProfile
            {
                WorldId = WorldId,
                CreatureId = _npc.Id,
                Description = "A watchful shopkeeper.",
                Appearance = new NpcAppearance { DistinguishingFeatures = ["A scar above one eye."] },
                Behavior = new NpcBehavior
                {
                    Personality = "Blunt and practical.",
                    SpeechStyle = "Clipped, direct sentences.",
                    Hobby = "woodworking",
                },
                PrivateBackground = new NpcPrivateBackground
                {
                    Origin = "Millhaven",
                    Profession = "Merchant",
                    Factions = [new NpcFaction(factionId, "The Ledger Guild")],
                    Family = [new NpcFamilyMember("Bram", "Brother")],
                    Home = "The Old Mill House",
                    Work = new NpcWorkBackground
                    {
                        Building = "General Store",
                        IsOwner = true,
                        Hours = "8am to 5pm",
                        DaysOff = ["Sunday"],
                    },
                },
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            MakeQuery(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("A watchful shopkeeper.", result.Appearance.Description);
        Assert.Equal("A scar above one eye.", Assert.Single(result.Appearance.DistinguishingFeatures));
        Assert.Equal("Blunt and practical.", result.Behavior.Personality);
        Assert.Equal("woodworking", result.Behavior.Hobby);
        Assert.Equal("Millhaven", result.PrivateBackground.Origin);
        Assert.Equal("Merchant", result.PrivateBackground.Profession);
        Assert.Equal("The Ledger Guild", Assert.Single(result.PrivateBackground.Factions));
        var family = Assert.Single(result.PrivateBackground.Family);
        Assert.Equal("Bram", family.Name);
        Assert.Equal("Brother", family.Relationship);
        Assert.Equal("The Old Mill House", result.PrivateBackground.Home);
        Assert.NotNull(result.PrivateBackground.Work);
        Assert.Equal("General Store", result.PrivateBackground.Work.Building);
        Assert.True(result.PrivateBackground.Work.IsOwner);
    }

    [Fact]
    public async Task Handle_FallsBackToBiographyDescription_WhenNoProfileExists()
    {
        // Arrange
        _npc.Biography = "A traveling merchant of few words.";
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            MakeQuery(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("A traveling merchant of few words.", result.Appearance.Description);
        Assert.Empty(result.Appearance.DistinguishingFeatures);
        Assert.Equal("", result.Behavior.Personality);
        Assert.Null(result.PrivateBackground.Work);
    }

    [Fact]
    public async Task Handle_ReturnsNeutralAttitude_WhenThereIsNoReputationHistory()
    {
        // Act
        var result = await _handler.Handle(
            MakeQuery(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("Neutral", result.RuntimeState.Attitude.Disposition);
    }

    [Fact]
    public async Task Handle_CombinesDirectAndFactionReputation_ForAttitude()
    {
        // Arrange
        var factionId = Guid.NewGuid();
        _context.NpcProfiles.Add(
            new NpcProfile
            {
                WorldId = WorldId,
                CreatureId = _npc.Id,
                PrivateBackground = new NpcPrivateBackground
                {
                    Factions = [new NpcFaction(factionId, "The Ledger Guild")],
                },
            }
        );
        _context.Reputations.AddRange(
            new Reputation
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = _npc.Id,
                TargetType = ReputationTargetType.Creature,
                Score = 20,
            },
            new Reputation
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = factionId,
                TargetType = ReputationTargetType.Faction,
                Score = 40,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            MakeQuery(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("Trusting", result.RuntimeState.Attitude.Disposition);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyHistory_WhenPlayerHasNeverSpokenToTheNpc()
    {
        // Act
        var result = await _handler.Handle(
            MakeQuery(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("", result.RuntimeState.ConversationHistory.Summary);
        Assert.Empty(result.RuntimeState.ConversationHistory.Recent);
    }

    [Fact]
    public async Task Handle_ReturnsTheFiveMostRecentConversations_InChronologicalOrder()
    {
        // Arrange
        var history = new NpcConversationHistory
        {
            WorldId = WorldId,
            CreatureId = _player.Id,
            NpcId = _npc.Id,
            Summary = "They have spoken many times.",
        };
        _context.NpcConversationHistories.Add(history);
        var now = DateTime.UtcNow;
        for (var i = 0; i < 7; i++)
        {
            _context.NpcConversations.Add(
                new NpcConversation
                {
                    WorldId = WorldId,
                    NpcConversationHistoryId = history.Id,
                    Summary = $"Conversation {i}",
                    CreatedAt = now.AddMinutes(i),
                }
            );
        }
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            MakeQuery(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("They have spoken many times.", result.RuntimeState.ConversationHistory.Summary);
        Assert.Equal(
            ["Conversation 2", "Conversation 3", "Conversation 4", "Conversation 5", "Conversation 6"],
            result.RuntimeState.ConversationHistory.Recent.Select(record => record.Summary)
        );
    }

    [Fact]
    public async Task Handle_SortsQuestsIntoTheCorrectBuckets()
    {
        // Arrange
        var available = Builders.MakeQuest(_npc.Id, WorldId);
        var active = Builders.MakeQuest(_npc.Id, WorldId);
        var ready = Builders.MakeQuest(_npc.Id, WorldId);
        var completed = Builders.MakeQuest(_npc.Id, WorldId);
        _context.Quests.AddRange(available, active, ready, completed);
        _context.CreatureQuests.AddRange(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = active.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            },
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = ready.Id,
                Status = QuestStatus.ReadyToComplete,
                WorldId = WorldId,
            },
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = completed.Id,
                Status = QuestStatus.Completed,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            MakeQuery(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(available.Name, Assert.Single(result.RuntimeState.Quests.Available).Name);
        Assert.Equal(active.Name, Assert.Single(result.RuntimeState.Quests.Active).Name);
        Assert.Equal(ready.Name, Assert.Single(result.RuntimeState.Quests.ReadyToComplete).Name);
        Assert.Equal(completed.Name, Assert.Single(result.RuntimeState.Quests.Completed).Name);
    }
}
