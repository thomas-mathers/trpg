using TRPG.Application.Reputations.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Reputations.Mappers;

public sealed class ReputationReasonMapperTests
{
    public static TheoryData<ReputationReason> AllReasons =>
        [.. Enum.GetValues<ReputationReason>()];

    // Guards the throwing default arm: a reason without display text crashes NPC briefings and
    // guard encounters, both of which render this for any logged entry.
    [Theory]
    [MemberData(nameof(AllReasons))]
    public void ToDisplayText_ReturnsText_ForEveryReason(ReputationReason reason)
    {
        // Act
        var text = reason.ToDisplayText();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(text));
    }
}
