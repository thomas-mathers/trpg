using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class QuestChainGeneratorValidationTests
{
    private static readonly string CreatureEntityId = Guid.NewGuid().ToString();
    private static readonly string LocationEntityId = Guid.NewGuid().ToString();
    private static readonly string ItemEntityId = Guid.NewGuid().ToString();
    private static readonly IReadOnlyDictionary<string, string> EntityTypesById = new Dictionary<
        string,
        string
    >
    {
        [CreatureEntityId] = QuestChainEntityTypes.Creature,
        [LocationEntityId] = QuestChainEntityTypes.Location,
        [ItemEntityId] = QuestChainEntityTypes.Item,
    };

    private static QuestChainObjectiveSchema MakeObjective(
        string objectiveType = nameof(GeneratedObjectiveType.ExploreLocation),
        string? targetEntityId = null,
        string? recipientEntityId = null,
        string? itemNameForKind = null,
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
                        ? LocationEntityId
                        : null
                ),
            RecipientEntityId = recipientEntityId,
            ItemNameForKind = itemNameForKind,
            CreatureTypeCategory = creatureTypeCategory,
            RequiredAmount = requiredAmount,
        };

    private static QuestChainNodeSchema MakeNode(
        string nodeId = "node-1",
        List<string>? prerequisiteNodeIds = null,
        string? giverEntityId = null,
        List<QuestChainObjectiveSchema>? objectives = null
    ) =>
        new()
        {
            NodeId = nodeId,
            Name = "Quest",
            Description = "A quest.",
            GiverEntityId = giverEntityId ?? CreatureEntityId,
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
        var schema = new QuestChainSchema { Nodes = [MakeNode(giverEntityId: LocationEntityId)] };

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
        // Arrange: KillCreature needs a Creature-type target, but this gives it a Location.
        var schema = new QuestChainSchema
        {
            Nodes =
            [
                MakeNode(
                    objectives:
                    [
                        MakeObjective(
                            objectiveType: nameof(GeneratedObjectiveType.KillCreature),
                            targetEntityId: LocationEntityId
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
            targetEntityId: ItemEntityId
        );
        var schema = new QuestChainSchema { Nodes = [MakeNode(objectives: [objective])] };

        // Act
        var error = QuestChainGenerator.Validate(schema, EntityTypesById);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenGiveItemsHasItemTargetAndCreatureRecipient()
    {
        // Arrange
        var objective = MakeObjective(
            objectiveType: nameof(GeneratedObjectiveType.GiveItems),
            targetEntityId: ItemEntityId,
            recipientEntityId: CreatureEntityId
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
