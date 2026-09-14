using TRPG.Creatures.Mappers;
using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Tests.Creatures.Mappers;

public class ItemMapperTests
{
    [Fact]
    public void ToDetail_MapsToMiscDetail_WhenTheItemHasNoSpecializedSubtype()
    {
        var item = new Item
        {
            WorldId = Guid.NewGuid(),
            Name = "Sealed Package",
            Description = "A sealed package addressed to someone else.",
            Quantity = 1,
        };

        var detail = item.ToDetail(isQuestItem: true);

        var miscDetail = Assert.IsType<MiscDetail>(detail);
        Assert.Equal(item.Id, miscDetail.ItemId);
        Assert.Equal(ItemType.Misc, miscDetail.Type);
        Assert.True(miscDetail.IsQuestItem);
    }
}
