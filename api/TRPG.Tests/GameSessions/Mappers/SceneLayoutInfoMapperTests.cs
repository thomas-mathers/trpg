using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;
using TRPG.GameSessions.Mappers;
using TRPG.GameSessions.Responses;
using DataBuildingType = TRPG.Domain.Models.BuildingType;
using DataPropModel = TRPG.Domain.Models.PropModel;
using WireBuildingType = TRPG.GameSessions.Responses.BuildingType;
using WirePropModel = TRPG.GameSessions.Responses.PropModel;

namespace TRPG.Tests.GameSessions.Mappers;

public sealed class SceneLayoutInfoMapperTests
{
    [Fact]
    public void ToWire_CarriesEveryPoseAndSize_WhenTheLayoutIsPopulated()
    {
        // Arrange
        var propId = Guid.NewGuid();
        var buildingId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var creatureId = Guid.NewGuid();
        var layout = new SceneLayoutInfo(
            new Footprint(40, 30),
            [
                new ScenePropLayout(
                    propId,
                    DataPropModel.ContainerChest,
                    new Placement(1, 2, 0.5),
                    new Footprint(2, 1)
                ),
            ],
            [
                new SceneBuildingLayout(
                    buildingId,
                    DataBuildingType.Tavern,
                    new Placement(20, 8, 1),
                    new Footprint(6, 4)
                ),
            ],
            [new SceneConnectorLayout(connectorId, destinationId, ExitX: 39, ExitY: 15)],
            [new SceneCreatureLayout(creatureId, new Placement(5, 6, 1.5))]
        );

        // Act
        var wire = layout.ToWire();

        // Assert
        var expected = new LocationLayoutWire(
            new FootprintWire(40, 30),
            [
                new PropLayoutWire(
                    propId,
                    WirePropModel.ContainerChest,
                    new PlacementWire(1, 2, 0.5),
                    new FootprintWire(2, 1)
                ),
            ],
            [
                new BuildingLayoutWire(
                    buildingId,
                    WireBuildingType.Tavern,
                    new PlacementWire(20, 8, 1),
                    new FootprintWire(6, 4)
                ),
            ],
            [new ConnectorLayoutWire(connectorId, destinationId, ExitX: 39, ExitY: 15)],
            [new CreatureLayoutWire(creatureId, new PlacementWire(5, 6, 1.5))]
        );
        Assert.Equal(expected.Size, wire.Size);
        Assert.Equal(expected.Props, wire.Props);
        Assert.Equal(expected.Buildings, wire.Buildings);
        Assert.Equal(expected.Connectors, wire.Connectors);
        Assert.Equal(expected.Creatures, wire.Creatures);
    }
}
