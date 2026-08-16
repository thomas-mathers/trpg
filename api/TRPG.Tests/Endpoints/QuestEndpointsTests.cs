using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts.Quests.Requests;
using TRPG.Contracts.Quests.Responses;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class QuestEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private TestApiClient _client = null!;
    private readonly World _world = Builders.MakeWorld();
    private Creature _player = null!;
    private Creature _giver = null!;
    private Quest _quest = null!;
    private QuestObjective _objective = null!;

    public async ValueTask InitializeAsync()
    {
        _client = fixture.CreateApiClient();
        _player = Builders.MakeCreature(_world.Id);
        _giver = Builders.MakeCreature(_world.Id);
        _quest = Builders.MakeQuest(_giver.Id, _world.Id);
        _objective = new CollectItemObjective
        {
            WorldId = _world.Id,
            QuestId = _quest.Id,
            Name = "Collect",
            Description = "Collect",
            ItemId = Guid.NewGuid(),
            RequiredAmount = 2,
        };
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        context.Worlds.Add(_world);
        context.Creatures.AddRange(_player, _giver);
        context.Quests.Add(_quest);
        context.QuestObjectives.Add(_objective);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task AcceptQuest_CreatesQuestProgress()
    {
        // Arrange
        var routeValues = new { playerId = _player.Id, questId = _quest.Id };

        // Act
        var response = await _client.PostAsync(
            "AcceptQuest",
            routeValues: routeValues,
            query: new Dictionary<string, object?> { ["worldId"] = _world.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        Assert.True(
            await context.CreatureQuestObjectives.AnyAsync(
                progress =>
                    progress.CreatureId == _player.Id && progress.ObjectiveId == _objective.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetQuestJournal_ReturnsObjectiveProgress()
    {
        // Arrange
        await SeedAcceptedQuest();

        // Act
        var response = await _client.GetAsync(
            "GetQuestJournal",
            new { playerId = _player.Id },
            new Dictionary<string, object?> { ["worldId"] = _world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var journal = await _client.ReadContentFromJsonAsync<QuestJournalEntrySnapshot[]>(
            response,
            TestContext.Current.CancellationToken
        );
        var quest = Assert.Single(journal!);
        Assert.Equal(_quest.Id, quest.Id);
        Assert.Equal(2, Assert.Single(quest.Objectives).RequiredAmount);
    }

    [Fact]
    public async Task SetQuestTracking_UpdatesTracking()
    {
        // Arrange
        await SeedAcceptedQuest();

        // Act
        var response = await _client.PutAsJsonAsync(
            "SetQuestTracking",
            new SetQuestTrackingRequest(false),
            new { playerId = _player.Id, questId = _quest.Id },
            new Dictionary<string, object?> { ["worldId"] = _world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var quest = await context.CreatureQuests.SingleAsync(
            quest => quest.CreatureId == _player.Id && quest.QuestId == _quest.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(quest.IsTracked);
    }

    private async Task SeedAcceptedQuest()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = _quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = _world.Id,
            }
        );
        context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = _player.Id,
                ObjectiveId = _objective.Id,
                Amount = 0,
                WorldId = _world.Id,
            }
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
