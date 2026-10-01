using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class AssetKeyResolverTests
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
    public void Resolve_ReturnsACatalogedKey_ForEveryWorkstationType(
        WorkstationType workstationType
    )
    {
        // Arrange
        var workstation = new Workstation { WorkstationType = workstationType };

        // Act
        var key = AssetKeyResolver.Resolve(workstation);

        // Assert
        Assert.True(PropFootprintCatalog.Contains(key));
    }

    [Theory]
    [MemberData(nameof(TrapKinds))]
    public void Resolve_ReturnsACatalogedKey_ForEveryTrapKind(TrapKind trapKind)
    {
        // Arrange
        var trap = new Trap { TrapKind = trapKind };

        // Act
        var key = AssetKeyResolver.Resolve(trap);

        // Assert
        Assert.True(PropFootprintCatalog.Contains(key));
    }

    [Theory]
    [MemberData(nameof(ConcretePropTypes))]
    public void Resolve_ReturnsACatalogedKey_ForEveryConcretePropType(Type propType)
    {
        // Arrange
        var prop = (Prop)Activator.CreateInstance(propType)!;

        // Act
        var key = AssetKeyResolver.Resolve(prop);

        // Assert
        Assert.True(PropFootprintCatalog.Contains(key));
    }

    [Theory]
    [InlineData("Chair", "prop.seat.chair")]
    [InlineData("Pew", "prop.seat.pew")]
    [InlineData("Throne", "prop.seat.throne")]
    [InlineData("Bench", "prop.seat.bench")]
    [InlineData("Stone Bench", "prop.seat.stone_bench")]
    [InlineData("Low Wall", "prop.seat.low_wall")]
    [InlineData("Comfy Sofa", "prop.seat.basic")]
    public void Resolve_ReturnsTheNamedSeatKey_WhenTheNameIsKnown(string name, string expectedKey)
    {
        // Arrange
        var seat = new Seat { Name = name };

        // Act
        var key = AssetKeyResolver.Resolve(seat);

        // Assert
        Assert.Equal(expectedKey, key);
    }

    [Theory]
    [InlineData("Barrel", "prop.container.barrel")]
    [InlineData("Chest", "prop.container.chest")]
    [InlineData("Crate", "prop.container.crate")]
    [InlineData("Footlocker", "prop.container.footlocker")]
    [InlineData("Weapon Rack", "prop.container.weapon_rack")]
    [InlineData("Strongbox", "prop.container.strongbox")]
    [InlineData("Mystery Box", "prop.container.basic")]
    public void Resolve_ReturnsTheNamedContainerKey_WhenTheNameIsKnown(
        string name,
        string expectedKey
    )
    {
        // Arrange
        var container = new Container { Name = name };

        // Act
        var key = AssetKeyResolver.Resolve(container);

        // Assert
        Assert.Equal(expectedKey, key);
    }

    [Theory]
    [InlineData("Lever", "prop.trigger.lever")]
    [InlineData("Gargoyle Eye", "prop.trigger.basic")]
    public void Resolve_ReturnsTheNamedTriggerKey_WhenTheNameIsKnown(
        string name,
        string expectedKey
    )
    {
        // Arrange
        var trigger = new Trigger { Name = name };

        // Act
        var key = AssetKeyResolver.Resolve(trigger);

        // Assert
        Assert.Equal(expectedKey, key);
    }

    [Fact]
    public void Resolve_ReturnsTheWorkstationTypeKey_ForAWorkstation()
    {
        // Arrange
        var workstation = new Workstation { WorkstationType = WorkstationType.Weaponsmithing };

        // Act
        var key = AssetKeyResolver.Resolve(workstation);

        // Assert
        Assert.Equal("prop.workstation.weaponsmithing", key);
    }
}
