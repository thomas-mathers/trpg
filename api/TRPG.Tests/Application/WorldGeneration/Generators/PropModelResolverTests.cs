using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class PropModelResolverTests
{
    public static TheoryData<WorkstationType> WorkstationTypes =>
        new(Enum.GetValues<WorkstationType>());

    public static TheoryData<TrapKind> TrapKinds => new(Enum.GetValues<TrapKind>());

    public static TheoryData<Type> ConcretePropTypes =>
        new(
            typeof(Prop)
                .Assembly.GetTypes()
                .Where(type => type.IsSubclassOf(typeof(Prop)) && !type.IsAbstract)
        );

    [Theory]
    [MemberData(nameof(WorkstationTypes))]
    public void Resolve_ReturnsACatalogedModel_ForEveryWorkstationType(
        WorkstationType workstationType
    )
    {
        // Arrange
        var workstation = new Workstation { WorkstationType = workstationType };

        // Act
        var model = PropModelResolver.Resolve(workstation);

        // Assert
        Assert.True(PropFootprintCatalog.Models.Contains(model));
    }

    [Theory]
    [MemberData(nameof(TrapKinds))]
    public void Resolve_ReturnsACatalogedModel_ForEveryTrapKind(TrapKind trapKind)
    {
        // Arrange
        var trap = new Trap { TrapKind = trapKind };

        // Act
        var model = PropModelResolver.Resolve(trap);

        // Assert
        Assert.True(PropFootprintCatalog.Models.Contains(model));
    }

    [Theory]
    [MemberData(nameof(ConcretePropTypes))]
    public void Resolve_ReturnsACatalogedModel_ForEveryConcretePropType(Type propType)
    {
        // Arrange
        var prop = (Prop)Activator.CreateInstance(propType)!;

        // Act
        var model = PropModelResolver.Resolve(prop);

        // Assert
        Assert.True(PropFootprintCatalog.Models.Contains(model));
    }

    [Theory]
    [InlineData("Chair", PropModel.SeatChair)]
    [InlineData("Pew", PropModel.SeatPew)]
    [InlineData("Throne", PropModel.SeatThrone)]
    [InlineData("Bench", PropModel.SeatBench)]
    [InlineData("Stone Bench", PropModel.SeatStoneBench)]
    [InlineData("Low Wall", PropModel.SeatLowWall)]
    [InlineData("Comfy Sofa", PropModel.SeatBasic)]
    public void Resolve_ReturnsTheNamedSeatModel_WhenTheNameIsKnown(
        string name,
        PropModel expectedModel
    )
    {
        // Arrange
        var seat = new Seat { Name = name };

        // Act
        var model = PropModelResolver.Resolve(seat);

        // Assert
        Assert.Equal(expectedModel, model);
    }

    [Theory]
    [InlineData("Barrel", PropModel.ContainerBarrel)]
    [InlineData("Chest", PropModel.ContainerChest)]
    [InlineData("Crate", PropModel.ContainerCrate)]
    [InlineData("Footlocker", PropModel.ContainerFootlocker)]
    [InlineData("Weapon Rack", PropModel.ContainerWeaponRack)]
    [InlineData("Strongbox", PropModel.ContainerStrongbox)]
    [InlineData("Mystery Box", PropModel.ContainerBasic)]
    public void Resolve_ReturnsTheNamedContainerModel_WhenTheNameIsKnown(
        string name,
        PropModel expectedModel
    )
    {
        // Arrange
        var container = new Container { Name = name };

        // Act
        var model = PropModelResolver.Resolve(container);

        // Assert
        Assert.Equal(expectedModel, model);
    }

    [Theory]
    [InlineData("Lever", PropModel.TriggerLever)]
    [InlineData("Gargoyle Eye", PropModel.TriggerBasic)]
    public void Resolve_ReturnsTheNamedTriggerModel_WhenTheNameIsKnown(
        string name,
        PropModel expectedModel
    )
    {
        // Arrange
        var trigger = new Trigger { Name = name };

        // Act
        var model = PropModelResolver.Resolve(trigger);

        // Assert
        Assert.Equal(expectedModel, model);
    }

    [Fact]
    public void Resolve_ReturnsTheWorkstationTypeModel_ForAWorkstation()
    {
        // Arrange
        var workstation = new Workstation { WorkstationType = WorkstationType.Weaponsmithing };

        // Act
        var model = PropModelResolver.Resolve(workstation);

        // Assert
        Assert.Equal(PropModel.WorkstationWeaponsmithing, model);
    }
}
