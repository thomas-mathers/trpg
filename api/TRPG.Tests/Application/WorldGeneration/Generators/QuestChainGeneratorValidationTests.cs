using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class QuestChainGeneratorValidationTests
{
    private static readonly string CreatureEntityId = Guid.NewGuid().ToString();
    private static readonly string DungeonEntityId = Guid.NewGuid().ToString();
    private static readonly string BuildingEntityId = Guid.NewGuid().ToString();
    private static readonly IReadOnlyDictionary<string, string> EntityTypesById = new Dictionary<
        string,
        string
    >
    {
        [CreatureEntityId] = QuestChainEntityTypes.Creature,
        [DungeonEntityId] = QuestChainEntityTypes.Dungeon,
        [BuildingEntityId] = QuestChainEntityTypes.Building,
    };

    private static QuestChainObjectiveSchema MakeObjective(
        string objectiveType = nameof(GeneratedObjectiveType.ExploreLocation),
        string? targetEntityId = null,
        string? recipientEntityId = null,
        string? itemNameForKind = null,
        string? newItemName = null,
        string? newPropName = null,
        string? creatureTypeCategory = null,
        int requiredAmount = 1
    ) =>
        new()
        {
            Name = "Objective",
            Description = "An objective.",
            ObjectiveType = objectiveType,
            TargetEntityId =
                targetEntityId
                ?? (
                    objectiveType == nameof(GeneratedObjectiveType.ExploreLocation)
                        ? DungeonEntityId
                        : null
                ),
            RecipientEntityId = recipientEntityId,
            ItemNameForKind = itemNameForKind,
            NewItemName = newItemName,
            NewPropName = newPropName,
            CreatureTypeCategory = creatureTypeCategory,
            RequiredAmount = requiredAmount,
        };

    private static QuestChainNodeSchema MakeNode(
        string nodeId = "node-1",
        List<string>? prerequisiteNodeIds = null,
        string? giverEntityId = null,
        string? requiredFactKey = null,
        List<QuestChainObjectiveSchema>? objectives = null
    ) =>
        new()
        {
            NodeId = nodeId,
            Name = "Quest",
            Description = "A quest.",
            GiverEntityId = giverEntityId ?? CreatureEntityId,
            RequiredFactKey = requiredFactKey,
            PrerequisiteNodeIds = prerequisiteNodeIds ?? [],
            Objectives = objectives ?? [MakeObjective()],
        };

    [Fact]
    public void Validate_ReturnsNull_WhenChainIsWellFormed()
    {
        // Arrange
        var schema = new QuestChainSchema { Nodes = [MakeNode()] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenSchemaHasNoNodes()
    {
        // Arrange
        var schema = new QuestChainSchema { Nodes = [] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenNodeIdIsDuplicated()
    {
        // Arrange
        var schema = new QuestChainSchema
        {
            Nodes = [MakeNode(nodeId: "node-1"), MakeNode(nodeId: "node-1")],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenPrerequisiteNodeIdDoesNotExist()
    {
        // Arrange
        var schema = new QuestChainSchema
        {
            Nodes = [MakeNode(nodeId: "node-1", prerequisiteNodeIds: ["node-missing"])],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenGraphContainsACycle()
    {
        // Arrange
        var schema = new QuestChainSchema
        {
            Nodes =
            [
                MakeNode(nodeId: "node-1", prerequisiteNodeIds: ["node-2"]),
                MakeNode(nodeId: "node-2", prerequisiteNodeIds: ["node-1"]),
            ],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenObjectiveTypeIsInvalid()
    {
        // Arrange
        var schema = new QuestChainSchema
        {
            Nodes = [MakeNode(objectives: [MakeObjective(objectiveType: "NotARealType")])],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenRequiredAmountIsLessThanOne()
    {
        // Arrange
        var schema = new QuestChainSchema
        {
            Nodes = [MakeNode(objectives: [MakeObjective(requiredAmount: 0)])],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenLearnFactDependsOnItsSupportingQuest()
    {
        // Arrange
        var learnFact = new QuestChainObjectiveSchema
        {
            Name = "Learn Mara's Secret",
            Description = "Ask Mara about the smugglers.",
            ObjectiveType = nameof(GeneratedObjectiveType.LearnFactFromCreature),
            TargetEntityId = CreatureEntityId,
            FactKey = "mara-secret",
            ReasonFactKey = "mara-fear",
            BaseWillingness = 10,
            BribeWillingness = 20,
            IntimidationWillingness = 20,
            WeightedSupportingQuestNodeIds =
            [
                new QuestChainSupportingQuestSchema { NodeId = "node-2", Weight = 30 },
            ],
        };
        var schema = new QuestChainSchema
        {
            Facts =
            [
                new QuestChainFactSchema
                {
                    Key = "mara-secret",
                    Subject = "Mara's secret",
                    Value = "Mara saw the smugglers leave town.",
                },
                new QuestChainFactSchema
                {
                    Key = "mara-fear",
                    Subject = "Mara's fear",
                    Value = "Mara fears the smugglers will hurt her brother.",
                },
            ],
            Nodes =
            [
                MakeNode(prerequisiteNodeIds: ["node-2"], objectives: [learnFact]),
                MakeNode(nodeId: "node-2", requiredFactKey: "mara-fear"),
            ],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenSupportingQuestDependsOnTheLearnFactNode()
    {
        // Arrange
        var learnFact = new QuestChainObjectiveSchema
        {
            Name = "Learn Mara's Secret",
            Description = "Ask Mara about the smugglers.",
            ObjectiveType = nameof(GeneratedObjectiveType.LearnFactFromCreature),
            TargetEntityId = CreatureEntityId,
            FactKey = "mara-secret",
            ReasonFactKey = "mara-fear",
            BaseWillingness = 10,
            BribeWillingness = 20,
            IntimidationWillingness = 20,
            WeightedSupportingQuestNodeIds =
            [
                new QuestChainSupportingQuestSchema { NodeId = "node-2", Weight = 30 },
            ],
        };
        var schema = new QuestChainSchema
        {
            Facts =
            [
                new QuestChainFactSchema
                {
                    Key = "mara-secret",
                    Subject = "Mara's secret",
                    Value = "Mara saw the smugglers leave town.",
                },
                new QuestChainFactSchema
                {
                    Key = "mara-fear",
                    Subject = "Mara's fear",
                    Value = "Mara fears the smugglers will hurt her brother.",
                },
            ],
            Nodes =
            [
                MakeNode(objectives: [learnFact]),
                MakeNode(
                    nodeId: "node-2",
                    prerequisiteNodeIds: ["node-1"],
                    requiredFactKey: "mara-fear"
                ),
            ],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenGiverEntityIdIsNull()
    {
        // Arrange
        var node = new QuestChainNodeSchema
        {
            NodeId = "node-1",
            Name = "Quest",
            Description = "A quest.",
            GiverEntityId = null,
            Objectives = [MakeObjective()],
        };
        var schema = new QuestChainSchema { Nodes = [node] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenGiverEntityIdIsNotInProvidedEntities()
    {
        // Arrange
        var schema = new QuestChainSchema
        {
            Nodes = [MakeNode(giverEntityId: Guid.NewGuid().ToString())],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenGiverEntityIdIsWrongType()
    {
        // Arrange
        var schema = new QuestChainSchema { Nodes = [MakeNode(giverEntityId: DungeonEntityId)] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenTargetEntityIdIsNotInProvidedEntities()
    {
        // Arrange
        var schema = new QuestChainSchema
        {
            Nodes =
            [
                MakeNode(objectives: [MakeObjective(targetEntityId: Guid.NewGuid().ToString())]),
            ],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenTargetEntityIdIsWrongType()
    {
        // Arrange: KillCreature needs a Creature-type target, but this gives it a Dungeon.
        var schema = new QuestChainSchema
        {
            Nodes =
            [
                MakeNode(
                    objectives:
                    [
                        MakeObjective(
                            objectiveType: nameof(GeneratedObjectiveType.KillCreature),
                            targetEntityId: DungeonEntityId
                        ),
                    ]
                ),
            ],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenClearLocationTargetsADungeon()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.ClearLocation),
            targetEntityId: DungeonEntityId
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenClearLocationTargetsABuilding()
    {
        // Arrange: a Building never has hostiles, so ClearLocation must reject it even though
        // ExploreLocation would accept the same entity.
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.ClearLocation),
            targetEntityId: BuildingEntityId
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenExploreLocationTargetsABuilding()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.ExploreLocation),
            targetEntityId: BuildingEntityId
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenGiveItemKindHasNoItemNameForKind()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.GiveItemKind),
            recipientEntityId: CreatureEntityId
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenGiveItemsHasNoRecipientEntityId()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.GiveItems),
            targetEntityId: CreatureEntityId,
            newItemName: "Signet Ring"
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenGiveItemsHasNoNewItemName()
    {
        // Arrange: items never pre-exist for GiveItems/CollectItem/DeliverItem — a new one must be
        // named, since TargetEntityId now identifies who holds it, not the item itself.
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.GiveItems),
            targetEntityId: CreatureEntityId,
            recipientEntityId: CreatureEntityId
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenGiveItemsTargetIsWrongType()
    {
        // Arrange: TargetEntityId is the holder now, so it must be a Creature, not a Dungeon.
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.GiveItems),
            targetEntityId: DungeonEntityId,
            recipientEntityId: CreatureEntityId,
            newItemName: "Signet Ring"
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenGiveItemsHasCreatureHolderNewItemNameAndRecipient()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.GiveItems),
            targetEntityId: CreatureEntityId,
            recipientEntityId: CreatureEntityId,
            newItemName: "Signet Ring"
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenCollectItemHasNoNewItemName()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.CollectItem),
            targetEntityId: CreatureEntityId
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenCollectItemHasCreatureHolderAndNewItemName()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.CollectItem),
            targetEntityId: CreatureEntityId,
            newItemName: "Ancient Coin"
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenKillCreatureHasNoTargetEntityId()
    {
        // Arrange
        var schema = new QuestChainSchema
        {
            Nodes =
            [
                MakeNode(
                    objectives:
                    [
                        MakeObjective(objectiveType: nameof(GeneratedObjectiveType.KillCreature)),
                    ]
                ),
            ],
        };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenKillCreatureTypeHasCategoryButNoTargetEntityId()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.KillCreatureType),
            creatureTypeCategory: "Goblin",
            requiredAmount: 3
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenKillCreatureTypeHasInvalidCategory()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.KillCreatureType),
            creatureTypeCategory: "NotARealCreatureType"
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenInteractWithPropHasNoNewPropName()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.InteractWithProp),
            targetEntityId: BuildingEntityId
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenInteractWithPropTargetsACreature()
    {
        // Arrange: the target identifies where the minted prop is placed, so it must be a
        // Dungeon or Building, not a Creature.
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.InteractWithProp),
            targetEntityId: CreatureEntityId,
            newPropName: "Cracked Well Valve"
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenInteractWithPropTargetsABuildingWithANewPropName()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.InteractWithProp),
            targetEntityId: BuildingEntityId,
            newPropName: "Cracked Well Valve"
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenInteractWithPropTargetsADungeonWithANewPropName()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.InteractWithProp),
            targetEntityId: DungeonEntityId,
            newPropName: "Warded Brazier"
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenNodeHasNoObjectives()
    {
        // Arrange
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }
}
