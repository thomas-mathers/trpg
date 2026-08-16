using TRPG.Application.Combat;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class PlayerCombatActionResolverTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void Resolve_ReturnsResolvedItem_WhenTheNamedItemExists()
    {
        // Arrange
        var potion = Builders.MakeConsumableItem(name: "Health Potion", amount: 20);
        var player = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithName("Hero")
            .AsPlayer()
            .WithItem(potion)
            .Build();
        IReadOnlyList<Combatant> combatants = [player];

        // Act
        var action = new PlayerCombatActionResolver(combatants).Resolve(
            new UseItemAction("Health Potion")
        );

        // Assert
        var resolvedItem = Assert.IsType<ResolvedUseItemAction>(action.Result);
        Assert.Equal(potion.Id, resolvedItem.Item.ItemId);
    }

    [Fact]
    public void Resolve_ReturnsActionRejected_WhenTheNamedItemDoesNotExist()
    {
        // Arrange
        var player = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithName("Hero")
            .AsPlayer()
            .Build();
        IReadOnlyList<Combatant> combatants = [player];

        // Act
        var action = new PlayerCombatActionResolver(combatants).Resolve(
            new UseItemAction("Health Potion")
        );

        // Assert
        Assert.Equal("Item Health Potion not found", action.ErrorMessage);
    }
}
