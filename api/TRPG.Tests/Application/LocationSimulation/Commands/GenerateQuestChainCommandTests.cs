using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class GenerateQuestChainCommandTests : IAsyncLifetime, IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly Location _giverLocation;
    private readonly Creature _giver;
    private readonly Location _dungeonExteriorLocation;
    private readonly Building _dungeon;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<GenerateQuestChainCommand, bool> _handler = null!;
    private FakeChatClient _chatClient = null!;

    public GenerateQuestChainCommandTests(DatabaseFixture database)
    {
        _database = database;
        _giverLocation = Builders.MakeLocation(_worldId, _stateId);
        _giver = Builders.MakeCreature(_worldId, locationId: _giverLocation.Id, name: "Giver");
        _dungeonExteriorLocation = Builders.MakeLocation(_worldId, _stateId);
        _dungeon = Builders.MakeBuilding(
            exteriorLocationId: _dungeonExteriorLocation.Id,
            worldId: _worldId,
            buildingType: BuildingType.Cave
        );
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _chatClient = new FakeChatClient();
        _services = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddKeyedSingleton<IChatClient>(LlmRoleKeys.QuestGeneration, _chatClient)
            .BuildServiceProvider();
        _handler = _services.GetRequiredService<ICommandHandler<GenerateQuestChainCommand, bool>>();

        _context.Locations.AddRange(_giverLocation, _dungeonExteriorLocation);
        _context.Creatures.Add(_giver);
        _context.Buildings.Add(_dungeon);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Guid> SeedPendingRequest()
    {
        var request = new QuestChainGenerationRequest
        {
            WorldId = _worldId,
            PlayerId = Guid.NewGuid(),
        };
        _context.QuestChainGenerationRequests.Add(request);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return request.Id;
    }

    [Fact]
    public async Task Handle_PersistsTheWholeChainWithPrerequisiteWiring_WhenGenerationSucceeds()
    {
        // Arrange
        var requestId = await SeedPendingRequest();
        _chatClient.QuestChainSchemaOverride = new QuestChainSchema
        {
            Nodes =
            [
                new QuestChainNodeSchema
                {
                    NodeId = "node-1",
                    Name = "Speak With Giver",
                    Description = "Talk to the giver.",
                    GiverEntityId = _giver.Id.ToString(),
                    Objectives =
                    [
                        new QuestChainObjectiveSchema
                        {
                            Name = "Speak",
                            Description = "Speak to the giver.",
                            ObjectiveType = nameof(GeneratedObjectiveType.SpeakToCreature),
                            TargetEntityId = _giver.Id.ToString(),
                        },
                    ],
                },
                new QuestChainNodeSchema
                {
                    NodeId = "node-2",
                    Name = "Clear The Dungeon",
                    Description = "Clear it out.",
                    GiverEntityId = _giver.Id.ToString(),
                    PrerequisiteNodeIds = ["node-1"],
                    Objectives =
                    [
                        new QuestChainObjectiveSchema
                        {
                            Name = "Clear",
                            Description = "Clear the dungeon.",
                            ObjectiveType = nameof(GeneratedObjectiveType.ClearLocation),
                            TargetEntityId = _dungeon.Id.ToString(),
                        },
                    ],
                },
            ],
        };

        // Act
        var result = await _handler.Handle(
            new GenerateQuestChainCommand
            {
                RequestId = requestId,
                ChainPremise = "A test premise.",
                ChainLength = 2,
                AvailableEntities =
                [
                    new QuestChainCandidateEntity(
                        _giver.Id,
                        _giver.Name,
                        QuestChainEntityTypes.Creature
                    ),
                    new QuestChainCandidateEntity(
                        _dungeon.Id,
                        _dungeon.Name,
                        QuestChainEntityTypes.Dungeon
                    ),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        var quests = await _context
            .Quests.Where(q => q.WorldId == _worldId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, quests.Count);
        var firstQuest = Assert.Single(quests, q => q.Name == "Speak With Giver");
        var secondQuest = Assert.Single(quests, q => q.Name == "Clear The Dungeon");
        Assert.Equal([firstQuest.Id], secondQuest.PrerequisiteQuestIds);
        var clearObjective = await _context
            .QuestObjectives.OfType<ClearLocationObjective>()
            .SingleAsync(o => o.QuestId == secondQuest.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_dungeon.Id, clearObjective.BuildingId);
        Assert.Equal(_dungeonExteriorLocation.Id, clearObjective.LocationId);
        var request = await _context.QuestChainGenerationRequests.SingleAsync(
            r => r.Id == requestId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(QuestChainGenerationStatus.Completed, request.Status);
    }

    [Fact]
    public async Task Handle_MintsANewItemOwnedByTheHolder_WhenObjectiveIsCollectItem()
    {
        // Arrange
        var requestId = await SeedPendingRequest();
        _chatClient.QuestChainSchemaOverride = new QuestChainSchema
        {
            Nodes =
            [
                new QuestChainNodeSchema
                {
                    NodeId = "node-1",
                    Name = "Recover The Signet",
                    Description = "Recover the signet ring.",
                    GiverEntityId = _giver.Id.ToString(),
                    Objectives =
                    [
                        new QuestChainObjectiveSchema
                        {
                            Name = "Collect Ring",
                            Description = "Take the signet ring.",
                            ObjectiveType = nameof(GeneratedObjectiveType.CollectItem),
                            TargetEntityId = _giver.Id.ToString(),
                            NewItemName = "Signet Ring",
                        },
                    ],
                },
            ],
        };

        // Act
        var result = await _handler.Handle(
            new GenerateQuestChainCommand
            {
                RequestId = requestId,
                ChainPremise = "A test premise.",
                ChainLength = 1,
                AvailableEntities =
                [
                    new QuestChainCandidateEntity(
                        _giver.Id,
                        _giver.Name,
                        QuestChainEntityTypes.Creature
                    ),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        var item = await _context.Items.SingleAsync(
            i => i.WorldId == _worldId && i.Name == "Signet Ring",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_giver.Id, item.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, item.Ownership.OwnerType);
        var collectObjective = await _context
            .QuestObjectives.OfType<CollectItemObjective>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(item.Id, collectObjective.ItemId);
    }

    [Fact]
    public async Task Handle_PersistsAuthoredFactsAndDisclosureObjectives_WhenGenerationSucceeds()
    {
        // Arrange
        var requestId = await SeedPendingRequest();
        _chatClient.QuestChainSchemaOverride = new QuestChainSchema
        {
            Facts =
            [
                new QuestChainFactSchema
                {
                    Key = "mara-shipment",
                    Subject = "The missing shipment",
                    Value = "Mara hid it in the quarry.",
                },
                new QuestChainFactSchema
                {
                    Key = "mara-debt",
                    Subject = "Why Mara refuses",
                    Value = "Smugglers hold Mara's brother over a debt.",
                },
            ],
            Nodes =
            [
                new QuestChainNodeSchema
                {
                    NodeId = "node-1",
                    Name = "Question Mara Closely",
                    Description = "Get Mara to reveal where the shipment went.",
                    GiverEntityId = _giver.Id.ToString(),
                    Objectives =
                    [
                        new QuestChainObjectiveSchema
                        {
                            Name = "Learn Mara's Secret",
                            Description = "Learn what Mara knows about the shipment.",
                            ObjectiveType = nameof(GeneratedObjectiveType.LearnFactFromCreature),
                            TargetEntityId = _giver.Id.ToString(),
                            FactKey = "mara-shipment",
                            ReasonFactKey = "mara-debt",
                            BaseWillingness = 10,
                            BribeWillingness = 35,
                            IntimidationWillingness = 20,
                            WeightedSupportingQuestNodeIds =
                            [
                                new QuestChainSupportingQuestSchema
                                {
                                    NodeId = "node-2",
                                    Weight = 40,
                                },
                            ],
                        },
                    ],
                },
                new QuestChainNodeSchema
                {
                    NodeId = "node-2",
                    Name = "Explore The Quarry",
                    Description = "Find the smugglers' leverage in the quarry.",
                    GiverEntityId = _giver.Id.ToString(),
                    RequiredFactKey = "mara-debt",
                    Objectives =
                    [
                        new QuestChainObjectiveSchema
                        {
                            Name = "Explore Quarry",
                            Description = "Reach the quarry entrance.",
                            ObjectiveType = nameof(GeneratedObjectiveType.ExploreLocation),
                            TargetEntityId = _dungeon.Id.ToString(),
                        },
                    ],
                },
            ],
        };

        // Act
        var result = await _handler.Handle(
            new GenerateQuestChainCommand
            {
                RequestId = requestId,
                ChainPremise = "A test premise.",
                ChainLength = 2,
                AvailableEntities =
                [
                    new QuestChainCandidateEntity(
                        _giver.Id,
                        _giver.Name,
                        QuestChainEntityTypes.Creature
                    ),
                    new QuestChainCandidateEntity(
                        _dungeon.Id,
                        _dungeon.Name,
                        QuestChainEntityTypes.Dungeon
                    ),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        var facts = await _context
            .Facts.Where(fact => fact.WorldId == _worldId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var shipmentFact = Assert.Single(facts, fact => fact.Subject == "The missing shipment");
        var debtFact = Assert.Single(facts, fact => fact.Subject == "Why Mara refuses");
        var objective = await _context
            .QuestObjectives.OfType<LearnFactFromCreatureObjective>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(shipmentFact.Id, objective.FactId);
        Assert.Equal(debtFact.Id, objective.ReasonFactId);
        var supportingQuest = await _context.Quests.SingleAsync(
            quest => quest.Name == "Explore The Quarry",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(debtFact.Id, supportingQuest.RequiredFactId);
        var support = Assert.Single(objective.WeightedSupportingQuestIds);
        Assert.Equal(supportingQuest.Id, support.QuestId);
        Assert.Equal(40, support.Weight);
    }

    [Fact]
    public async Task Handle_MarksTheRequestFailed_WhenGenerationNeverProducesValidOutput()
    {
        // Arrange — an empty chain never passes validation, so GetValidatedJson exhausts its retries
        var requestId = await SeedPendingRequest();
        _chatClient.QuestChainSchemaOverride = new QuestChainSchema { Nodes = [] };

        // Act
        var result = await _handler.Handle(
            new GenerateQuestChainCommand
            {
                RequestId = requestId,
                ChainPremise = "A test premise.",
                ChainLength = 1,
                AvailableEntities =
                [
                    new QuestChainCandidateEntity(
                        _giver.Id,
                        _giver.Name,
                        QuestChainEntityTypes.Creature
                    ),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
        var request = await _context.QuestChainGenerationRequests.SingleAsync(
            r => r.Id == requestId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(QuestChainGenerationStatus.Failed, request.Status);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenTheRequestDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GenerateQuestChainCommand
            {
                RequestId = Guid.NewGuid(),
                ChainPremise = "A test premise.",
                ChainLength = 1,
                AvailableEntities = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenTheRequestIsNoLongerPending()
    {
        // Arrange
        var requestId = await SeedPendingRequest();
        var request = await _context.QuestChainGenerationRequests.SingleAsync(
            request => request.Id == requestId,
            TestContext.Current.CancellationToken
        );
        request.Status = QuestChainGenerationStatus.Failed;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GenerateQuestChainCommand
            {
                RequestId = requestId,
                ChainPremise = "A test premise.",
                ChainLength = 1,
                AvailableEntities = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }
}
